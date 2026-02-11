using BMIRussian_ru.Data;
using Meilisearch;

namespace BMIRussian_ru.Services;

public class MeilisearchService : IMeilisearchService
{
    private const string VideosIndexName = "videos";
    private readonly MeilisearchClient? _client;
    private readonly ILogger<MeilisearchService> _logger;

    public MeilisearchService(Microsoft.Extensions.Options.IOptions<MeilisearchOptions> options, ILogger<MeilisearchService> logger)
    {
        _logger = logger;
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Host))
        {
            _client = null;
            return;
        }

        try
        {
            _logger.LogInformation($"Meilisearch at server {opts.Host}");
            _client = new MeilisearchClient(opts.Host, opts.ApiKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Meilisearch client. Search indexing will be disabled.");
            _client = null;
        }
    }

    public async Task IndexVideoAsync(Video video, CancellationToken cancellationToken = default)
    {
        if (_client == null) return;

        try
        {
            var doc = ToSearchDocument(video);
            var index = _client.Index(VideosIndexName);
            await index.AddDocumentsAsync(new[] { doc }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index video {VideoId} in Meilisearch", video.Id);
        }
    }

    public async Task IndexVideosAsync(IEnumerable<Video> videos, CancellationToken cancellationToken = default)
    {
        if (_client == null) return;

        var list = videos.ToList();
        if (list.Count == 0) return;

        try
        {
            var docs = list.Select(ToSearchDocument).ToArray();
            var index = _client.Index(VideosIndexName);
            await index.AddDocumentsAsync(docs, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index {Count} videos in Meilisearch", list.Count);
        }
    }

    public async Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
    {
        if (_client == null) return;

        try
        {
            var index = _client.Index(VideosIndexName);
            await index.DeleteOneDocumentAsync(videoId.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete video {VideoId} from Meilisearch", videoId);
        }
    }

    public async Task<VideoSearchResult> SearchVideosAsync(string query, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrWhiteSpace(query))
            return new VideoSearchResult(Array.Empty<long>(), 0);

        try
        {
            var index = _client.Index(VideosIndexName);
            var searchQuery = new SearchQuery { Limit = limit, Offset = offset };
            var result = await index.SearchAsync<VideoSearchDocument>(query.Trim(), searchQuery, cancellationToken);

            var ids = new List<long>();
            foreach (var hit in result.Hits)
            {
                if (long.TryParse(hit.Id, out var id))
                    ids.Add(id);
            }
            var estimatedTotal = result is SearchResult<VideoSearchDocument> sr ? sr.EstimatedTotalHits : ids.Count;
            return new VideoSearchResult(ids, estimatedTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search videos in Meilisearch for query: {Query}", query);
            return new VideoSearchResult(Array.Empty<long>(), 0);
        }
    }

    private static VideoSearchDocument ToSearchDocument(Video video) =>
        new()
        {
            Id = video.Id.ToString(),
            Title = video.Title ?? "",
            Description = video.Description ?? ""
        };

    private sealed class VideoSearchDocument
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
