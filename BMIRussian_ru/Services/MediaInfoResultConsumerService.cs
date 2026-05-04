using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BMIRussian_ru.Services;

/// <summary>
/// Consumes from the Downloader result topic and stores MediaInfoResult/MediaInfoFailed in MediaInfoResultStore.
/// </summary>
public class MediaInfoResultConsumerService(
    IOptions<DownloaderKafkaOptions> options,
    MediaInfoResultStore store,
    ILogger<MediaInfoResultConsumerService> logger) : BackgroundService
{
    private readonly DownloaderKafkaOptions _options = options?.Value ?? new DownloaderKafkaOptions();
    private readonly MediaInfoResultStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ILogger<MediaInfoResultConsumerService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Downloader Kafka result topic not configured; MediaInfo result consumer disabled");
            return;
        }

        await EnsureTopicExistsAsync(stoppingToken);

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
                            _logger.LogInformation("Video parser {RequestId}", msg.RequestId);
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
                            _logger.LogInformation("Video failed to parse {RequestId}", msg.RequestId);
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

    private async Task EnsureTopicExistsAsync(CancellationToken cancellationToken)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers,
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
            config.SaslUsername = _options.Username;
            config.SaslPassword = _options.Password;
        }

        using var admin = new AdminClientBuilder(config).Build();
        try
        {
            await admin.CreateTopicsAsync(
                new[] { new TopicSpecification { Name = _options.ResultTopic, NumPartitions = 1, ReplicationFactor = 1 } },
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });
            _logger.LogInformation("Created Kafka topic {Topic} for MediaInfo results", _options.ResultTopic);
        }
        catch (CreateTopicsException ex) when (ex.Results.Count > 0)
        {
            var result = ex.Results[0];
            if (result.Error.Code == ErrorCode.TopicAlreadyExists)
                _logger.LogDebug("Kafka topic {Topic} already exists", _options.ResultTopic);
            else
                _logger.LogWarning(ex, "Could not ensure topic {Topic} exists: {Reason}", _options.ResultTopic, result.Error.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure topic {Topic} exists; consumer may fail if topic is not created", _options.ResultTopic);
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
