using BMIRussian_ru.Data;

namespace BMIRussian_ru.Services;

public interface IMeilisearchService
{
    /// <summary>
    /// Index or update a video in Meilisearch (id, title, description).
    /// </summary>
    Task IndexVideoAsync(Video video, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index multiple videos in Meilisearch.
    /// </summary>
    Task IndexVideosAsync(IEnumerable<Video> videos, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a video from the Meilisearch index.
    /// </summary>
    Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default);
}
