namespace BMIRussian_ru.Services;

public interface IMediaInfoKafkaService
{
    /// <summary>
    /// Returns null if Kafka is not configured or request failed/timeout.
    /// </summary>
    Task<MediaInfoResult?> GetVideoMetadataAsync(string url, CancellationToken cancellationToken = default);
}
