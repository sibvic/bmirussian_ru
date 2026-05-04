using System.Text.RegularExpressions;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BMIRussian_ru.Services;

public class MediaInfoKafkaService : IMediaInfoKafkaService, IDisposable
{
    private readonly DownloaderKafkaOptions _options;
    private readonly MediaInfoResultStore _store;
    private readonly ILogger<MediaInfoKafkaService> _logger;
    private readonly Lazy<IProducer<string, string>> _producer;

    public MediaInfoKafkaService(
        IOptions<DownloaderKafkaOptions> options,
        MediaInfoResultStore store,
        ILogger<MediaInfoKafkaService> logger)
    {
        _options = options?.Value ?? new DownloaderKafkaOptions();
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producer = new Lazy<IProducer<string, string>>(CreateProducer, isThreadSafe: true);
    }

    public async Task<MediaInfoResult?> GetVideoMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogDebug("Downloader Kafka is not configured; skipping MediaInfo request");
            return null;
        }

        var requestId = Guid.NewGuid().ToString("N");
        _store.RegisterPending(requestId);

        var message = new { RequestId = requestId, Url = url };
        var json = JsonConvert.SerializeObject(message);
        try
        {
            var producer = _producer.Value;
            await producer.ProduceAsync(
                _options.Topic,
                new Message<string, string> { Key = "MediaInfo", Value = json },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send MediaInfo request for {Url}", url);
            _store.SetFailed(requestId, ex.Message);
        }

        return await _store.WaitForResultAsync(requestId, TimeSpan.FromSeconds(60), cancellationToken);
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

    public void Dispose() => GC.SuppressFinalize(this);
}
