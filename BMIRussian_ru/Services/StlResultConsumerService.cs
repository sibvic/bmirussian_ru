using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BMIRussian_ru.Services;

/// <summary>
/// Consumes StlResult/StlFailed messages from the StlWorker result topic and updates StlJobStore.
/// </summary>
public class StlResultConsumerService(
    IOptions<ShopKafkaOptions> options,
    IServiceScopeFactory scopeFactory,
    MetricsService metrics,
    ILogger<StlResultConsumerService> logger) : BackgroundService
{
    private readonly ShopKafkaOptions _options = options?.Value ?? new ShopKafkaOptions();
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly MetricsService _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    private readonly ILogger<StlResultConsumerService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("ShopKafka result topic not configured; STL result consumer disabled");
            return;
        }

        await EnsureTopicExistsAsync(stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = "BMIRussian_ru-StlResult",
            AutoOffsetReset = AutoOffsetReset.Earliest,
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
        _logger.LogInformation("STL result consumer subscribed to {Topic}", _options.ResultTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(stoppingToken);
                    var key = cr.Message.Key;
                    var value = cr.Message.Value ?? "";

                    if (key == "StlResult")
                    {
                        try
                        {
                            var msg = JsonConvert.DeserializeObject<StlResultMessage>(value);
                            _logger.LogInformation("STL generated {RequestId}", msg?.RequestId);
                            if (msg != null && !string.IsNullOrEmpty(msg.RequestId))
                            {
                                await using var scope = _scopeFactory.CreateAsyncScope();
                                var store = scope.ServiceProvider.GetRequiredService<StlJobStore>();
                                var job = await store.SetDoneAsync(msg.RequestId, msg.ObjectName ?? "");
                                if (job != null)
                                    _metrics.IncrementStlJobs(job.Model, "done");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to handle StlResult: {Value}", value);
                        }
                    }
                    else if (key == "StlFailed")
                    {
                        try
                        {
                            var msg = JsonConvert.DeserializeObject<StlFailedMessage>(value);
                            _logger.LogInformation("STL generation failed {RequestId}", msg?.RequestId);
                            if (msg != null && !string.IsNullOrEmpty(msg.RequestId))
                            {
                                await using var scope = _scopeFactory.CreateAsyncScope();
                                var store = scope.ServiceProvider.GetRequiredService<StlJobStore>();
                                var job = await store.SetFailedAsync(msg.RequestId, msg.Error ?? "Unknown error");
                                if (job != null)
                                    _metrics.IncrementStlJobs(job.Model, "failed");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to handle StlFailed: {Value}", value);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in STL result consumer");
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
            _logger.LogInformation("Created Kafka topic {Topic} for STL results", _options.ResultTopic);
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

    private class StlResultMessage
    {
        public string RequestId { get; set; } = "";
        public string? ObjectName { get; set; }
    }

    private class StlFailedMessage
    {
        public string RequestId { get; set; } = "";
        public string? Error { get; set; }
    }
}
