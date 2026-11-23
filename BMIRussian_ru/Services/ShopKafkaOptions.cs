namespace BMIRussian_ru.Services;

/// <summary>
/// Kafka configuration for sending STL generation requests to the StlWorker and receiving results.
/// </summary>
public class ShopKafkaOptions
{
    public const string SectionName = "ShopKafka";

    public string BootstrapServers { get; set; } = string.Empty;
    /// <summary>Topic the StlWorker consumes from (we produce GenerateStl messages here).</summary>
    public string Topic { get; set; } = string.Empty;
    /// <summary>Dedicated topic for StlResult/StlFailed messages.</summary>
    public string ResultTopic { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BootstrapServers) && !string.IsNullOrWhiteSpace(Topic) && !string.IsNullOrWhiteSpace(ResultTopic);
}
