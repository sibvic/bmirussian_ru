using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BMIRussian_ru.Services;

/// <summary>
/// Produces GenerateStl messages consumed by the external StlWorker service.
/// </summary>
public class StlKafkaService : IDisposable
{
    private readonly ShopKafkaOptions _options;
    private readonly ILogger<StlKafkaService> _logger;
    private readonly Lazy<IProducer<string, string>> _producer;

    public StlKafkaService(IOptions<ShopKafkaOptions> options, ILogger<StlKafkaService> logger)
    {
        _options = options?.Value ?? new ShopKafkaOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producer = new Lazy<IProducer<string, string>>(CreateProducer, isThreadSafe: true);
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<bool> RequestGenerateAsync(string requestId, string model, string text, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("ShopKafka is not configured; cannot send GenerateStl request {RequestId}", requestId);
            return false;
        }

        var message = new { RequestId = requestId, Model = model, Text = text };
        var json = JsonConvert.SerializeObject(message);
        try
        {
            _logger.LogInformation("Sending GenerateStl request {RequestId} for model {Model}", requestId, model);
            await _producer.Value.ProduceAsync(
                _options.Topic,
                new Message<string, string> { Key = "GenerateStl", Value = json },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send GenerateStl request {RequestId}", requestId);
            return false;
        }
    }

    private IProducer<string, string> CreateProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
            config.SaslUsername = _options.Username;
            config.SaslPassword = _options.Password;
        }
        return new ProducerBuilder<string, string>(config).Build();
    }

    public void Dispose()
    {
        if (_producer.IsValueCreated)
        {
            try
            {
                _producer.Value.Flush(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush STL Kafka producer");
            }
            _producer.Value.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
