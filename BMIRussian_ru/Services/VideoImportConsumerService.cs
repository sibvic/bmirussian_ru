using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BMIRussian_ru.Services;

/// <summary>
/// Consumes VideoImport messages produced by the Telegram bot when a video post
/// appears in the configured channel, and imports the video (auto-published).
/// </summary>
public class VideoImportConsumerService(
    IServiceProvider serviceProvider,
    IOptions<KafkaMessageSenderOptions> options,
    IMeilisearchService meilisearch,
    MetricsService metrics,
    ILogger<VideoImportConsumerService> logger) : BackgroundService
{
    private readonly KafkaMessageSenderOptions _options = options?.Value ?? new KafkaMessageSenderOptions();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);
        if (string.IsNullOrWhiteSpace(_options.BootstrapServers) || string.IsNullOrWhiteSpace(_options.InTopic))
        {
            logger.LogInformation("VideoImport consumer disabled: Kafka BootstrapServers/InTopic not configured");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = "BMIRussian_ru-VideoImport",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
            config.SaslUsername = _options.Username;
            config.SaslPassword = _options.Password;
        }

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_options.InTopic);
        logger.LogInformation("VideoImport consumer subscribed to {Topic}", _options.InTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(stoppingToken);
                    if (cr?.Message == null || cr.Message.Key != "VideoImport")
                        continue;
                    await HandleVideoImportAsync(cr.Message.Value ?? "", stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consumption error in VideoImport consumer");
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in VideoImport consumer loop");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleVideoImportAsync(string value, CancellationToken cancellationToken)
    {
        VideoImportMessage? msg;
        try
        {
            msg = JsonConvert.DeserializeObject<VideoImportMessage>(value);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize VideoImport message: {Value}", value);
            metrics.IncrementVideosImported("error", "telegram");
            return;
        }

        if (msg == null || string.IsNullOrWhiteSpace(msg.VkUrl))
        {
            logger.LogWarning("Invalid VideoImport message (missing VkUrl): {Value}", value);
            metrics.IncrementVideosImported("error", "telegram");
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<VideoImportService>();
        var result = await importer.ImportFromTelegramAsync(
            msg.Title ?? "",
            msg.VkUrl,
            msg.YouTubeUrl,
            msg.Description,
            msg.TelegramMessageId,
            msg.PostDateUtc);

        if (result.Duplicate)
        {
            metrics.IncrementVideosImported("duplicate", "telegram");
            return;
        }
        if (result.Error != null || result.Video == null)
        {
            logger.LogWarning("VideoImport failed for {Url}: {Error}", msg.VkUrl, result.Error);
            metrics.IncrementVideosImported("error", "telegram");
            return;
        }

        try
        {
            await meilisearch.IndexVideoAsync(result.Video, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Meilisearch indexing failed for imported video {VideoId}", result.Video.Id);
        }
        metrics.IncrementVideosPublished();
        metrics.IncrementVideosImported("success", "telegram");
        logger.LogInformation("Auto-published video {VideoId} '{Title}' from telegram post {MessageId}",
            result.Video.Id, result.Video.Title, msg.TelegramMessageId);
    }

    private class VideoImportMessage
    {
        public string? Title { get; set; }
        public string? VkUrl { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? Description { get; set; }
        public long TelegramMessageId { get; set; }
        public DateTime PostDateUtc { get; set; }
    }
}
