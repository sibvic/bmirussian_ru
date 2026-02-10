namespace BMIRussian_ru.Services;

/// <summary>
/// Kafka configuration for sending MediaInfo requests to the Downloader and receiving results.
/// </summary>
public class DownloaderKafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
    /// <summary>Topic the Downloader consumes from (we produce MediaInfo messages here).</summary>
    public string Topic { get; set; } = string.Empty;
    /// <summary>Dedicated topic for MediaInfo results (MediaInfoResult/MediaInfoFailed). Must match Downloader's MediaInfoOutputTopic.</summary>
    public string ResultTopic { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BootstrapServers) && !string.IsNullOrWhiteSpace(Topic) && !string.IsNullOrWhiteSpace(ResultTopic);
}
