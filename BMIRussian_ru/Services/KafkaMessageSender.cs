using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace BMIRussian_ru.Services
{
    public class KafkaMessageSender : IDisposable
    {
        private readonly ILogger<KafkaMessageSender> logger;
        private readonly KafkaMessageSenderOptions options;
        private readonly Lazy<IProducer<string, string>> producerFactory;

        public KafkaMessageSender(IOptions<KafkaMessageSenderOptions> optionsAccessor, ILogger<KafkaMessageSender> logger)
        {
            ArgumentNullException.ThrowIfNull(optionsAccessor);
            ArgumentNullException.ThrowIfNull(logger);

            options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
            if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            {
                throw new ArgumentException("Kafka bootstrap servers must be provided.", nameof(optionsAccessor));
            }

            if (string.IsNullOrWhiteSpace(options.OutTopic))
            {
                throw new ArgumentException("Kafka topic must be provided.", nameof(optionsAccessor));
            }

            this.logger = logger;
            producerFactory = new Lazy<IProducer<string, string>>(CreateProducer, isThreadSafe: true);
        }

        public void SendMessage(string message, string channelId)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(channelId))
            {
                logger.LogWarning("Skipping Kafka publish because channelId is missing.");
                return;
            }

            try
            {
                logger.LogInformation("Publishing Kafka message to topic {Topic} with key {Key} and message {Message}", options.OutTopic, channelId, message);
                var producer = producerFactory.Value;
                var deliveryTask = producer.ProduceAsync(
                    options.OutTopic,
                    new Message<string, string>
                    {
                        Key = channelId,
                        Value = message
                    });

                deliveryTask.GetAwaiter().GetResult();
            }
            catch (ProduceException<string, string> ex)
            {
                logger.LogError(ex, "Kafka delivery failed for topic {Topic} and key {Key}", options.OutTopic, channelId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while publishing Kafka message for topic {Topic}", options.OutTopic);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (producerFactory.IsValueCreated)
            {
                try
                {
                    producerFactory.Value.Flush(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to flush Kafka producer.");
                }

                producerFactory.Value.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private IProducer<string, string> CreateProducer()
        {
            ProducerConfig config = options.AdditionalConfig is { Count: > 0 }
                ? new ProducerConfig(new Dictionary<string, string>(options.AdditionalConfig))
                : new ProducerConfig();

            config.BootstrapServers = options.BootstrapServers;

            if (!string.IsNullOrWhiteSpace(options.ClientId))
            {
                config.ClientId = options.ClientId;
            }

            if (options.MessageTimeoutMs.HasValue)
            {
                config.MessageTimeoutMs = options.MessageTimeoutMs.Value;
            }

            if (options.SocketTimeoutMs.HasValue)
            {
                config.SocketTimeoutMs = options.SocketTimeoutMs.Value;
            }
            config.SaslMechanism = SaslMechanism.Plain;
            config.SecurityProtocol = SecurityProtocol.SaslPlaintext;
            config.SaslUsername = options.Username;
            config.SaslPassword = options.Password;

            return new ProducerBuilder<string, string>(config).Build();
        }
    }
}

