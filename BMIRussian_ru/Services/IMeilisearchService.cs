using BMIRussian_ru.Data;

namespace BMIRussian_ru.Services;

public interface IMeilisearchService
{
    /// <summary>
    /// Index or update a video in Meilisearch (id, title, description).
    /// </summary>
    Task IndexVideoAsync(Video video, string? transcriptContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index multiple videos in Meilisearch.
    /// </summary>
    Task IndexVideosAsync(IEnumerable<Video> videos, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a video from the Meilisearch index.
    /// </summary>
    Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search videos by query. Returns video IDs matching the search.
    /// </summary>
    Task<VideoSearchResult> SearchVideosAsync(string query, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
}

public record VideoSearchResult(IReadOnlyList<long> VideoIds, int EstimatedTotalHits);
