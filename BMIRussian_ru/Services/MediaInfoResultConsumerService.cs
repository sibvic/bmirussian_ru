using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BMIRussian_ru.Services;

/// <summary>
/// Consumes from the Downloader result topic and stores MediaInfoResult/MediaInfoFailed in MediaInfoResultStore.
/// </summary>
public class MediaInfoResultConsumerService : BackgroundService
{
    private readonly DownloaderKafkaOptions _options;
    private readonly MediaInfoResultStore _store;
    private readonly ILogger<MediaInfoResultConsumerService> _logger;

    public MediaInfoResultConsumerService(
        IOptions<DownloaderKafkaOptions> options,
        MediaInfoResultStore store,
        ILogger<MediaInfoResultConsumerService> logger)
    {
        _options = options?.Value ?? new DownloaderKafkaOptions();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Downloader Kafka result topic not configured; MediaInfo result consumer disabled");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = "BMIRussian_ru-MediaInfoResult",
            AutoOffsetReset = AutoOffsetReset.Latest,
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
            config.SaslUsername = _options.Username;
            config.SaslPassword = _options.Password;
        }

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_options.ResultTopic);
        _logger.LogInformation("MediaInfo result consumer subscribed to {Topic}", _options.ResultTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(stoppingToken);
                    var key = cr.Message.Key;
                    var value = cr.Message.Value ?? "";

                    if (key == "MediaInfoResult")
                    {
                        try
                        {
                            var msg = JsonConvert.DeserializeObject<MediaInfoResultMessage>(value);
                            if (msg != null && !string.IsNullOrEmpty(msg.RequestId))
                            {
                                _store.SetResult(msg.RequestId, new MediaInfoResult
                                {
                                    Title = msg.Title,
                                    Thumbnail = msg.Thumbnail,
                                    Description = msg.Description,
                                    PublishDate = msg.PublishDate
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize MediaInfoResult: {Value}", value);
                        }
                    }
                    else if (key == "MediaInfoFailed")
                    {
                        try
                        {
                            var msg = JsonConvert.DeserializeObject<MediaInfoFailedMessage>(value);
                            if (msg != null && !string.IsNullOrEmpty(msg.RequestId))
                                _store.SetFailed(msg.RequestId, msg.Error ?? "Unknown error");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize MediaInfoFailed: {Value}", value);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in MediaInfo result consumer");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private class MediaInfoResultMessage
    {
        public string RequestId { get; set; } = "";
        public string? Title { get; set; }
        public string? Thumbnail { get; set; }
        public string? Description { get; set; }
        public DateTime PublishDate { get; set; }
    }

    private class MediaInfoFailedMessage
    {
        public string RequestId { get; set; } = "";
        public string? Error { get; set; }
    }
}
