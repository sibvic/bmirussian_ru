using System.Globalization;
using BMIRussian_ru.AuthLib.Models;
using BMIRussian_ru.Data;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Services
{
    public class LoginService(IServiceProvider serviceProvider,
        IOptions<KafkaMessageSenderOptions> kafkaOptionsAccessor,
        ILogger<LoginService> logger) : BackgroundService
    {
        private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly ILogger<LoginService> logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly KafkaMessageSenderOptions kafkaOptions = kafkaOptionsAccessor?.Value
            ?? throw new ArgumentNullException(nameof(kafkaOptionsAccessor));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);
            if (string.IsNullOrWhiteSpace(kafkaOptions.BootstrapServers)
                || string.IsNullOrWhiteSpace(kafkaOptions.InTopic))
            {
                var missingConfig = new List<string>();
                if (string.IsNullOrWhiteSpace(kafkaOptions.BootstrapServers))
                    missingConfig.Add("BootstrapServers");
                if (string.IsNullOrWhiteSpace(kafkaOptions.InTopic))
                    missingConfig.Add("InTopic");
                
                logger.LogWarning("Kafka users consumer is disabled because configuration is missing: {MissingConfig}",
                    string.Join(", ", missingConfig));
                return;
            }

            var consumerConfig = CreateConsumerConfig();

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
                .SetErrorHandler((_, error) =>
                {
                    if (error.IsError)
                    {
                        logger.LogError("Kafka users consumer error: {Error}", error);
                    }
                })
                .Build();

            consumer.Subscribe(kafkaOptions.InTopic);
            logger.LogInformation("Kafka users consumer subscribed to topic {Topic}.", kafkaOptions.InTopic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    if (consumeResult?.Message == null)
                    {
                        continue;
                    }

                    await HandleMessageAsync(consumeResult.Message.Key, consumeResult.Message.Value, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consumption error while reading users topic {Topic}.", kafkaOptions.InTopic);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in users Kafka consumer loop.");
                }
            }

            try
            {
                consumer.Close();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to close users Kafka consumer gracefully.");
            }
        }

        private ConsumerConfig CreateConsumerConfig()
        {
            var groupId = string.IsNullOrWhiteSpace(kafkaOptions.ClientId)
                ? "bmirussian-ru-login"
                : $"{kafkaOptions.ClientId}-login";

            return new ConsumerConfig
            {
                BootstrapServers = kafkaOptions.BootstrapServers,
                GroupId = groupId,
                ClientId = $"{groupId}-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                SaslMechanism = SaslMechanism.Plain,
                SecurityProtocol = SecurityProtocol.SaslPlaintext,
                SaslUsername = kafkaOptions.Username,
                SaslPassword = kafkaOptions.Password
            };
        }

        private async Task HandleMessageAsync(string? key, string? value, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                logger.LogWarning("Received users Kafka message without key.");
                return;
            }

            if (string.Equals(key, nameof(RegistrationRequest), StringComparison.OrdinalIgnoreCase))
            {
                await HandleRegistrationRequestMessageAsync(value, cancellationToken);
                return;
            }

            if (string.Equals(key, nameof(AuthRequest), StringComparison.OrdinalIgnoreCase))
            {
                await HandleAuthRequestMessageAsync(value, cancellationToken);
                return;
            }
        }

        private async Task HandleRegistrationRequestMessageAsync(string? value, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                logger.LogWarning("Received RegistrationRequest message without payload.");
                return;
            }

            RegistrationRequest? payload;
            try
            {
                payload = JsonConvert.DeserializeObject<RegistrationRequest>(value);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize RegistrationRequest message: {Payload}", value);
                return;
            }

            if (payload == null)
            {
                logger.LogWarning("Deserialized RegistrationRequest payload is null.");
                return;
            }

            await ProcessRegistrationRequestAsync(payload, cancellationToken);
        }

        private async Task ProcessRegistrationRequestAsync(RegistrationRequest payload, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var telegramId = payload.TelegramUserId.ToString(CultureInfo.InvariantCulture);

            var existingCredential = await context.UserCredentianls
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.Source == CredentialsSource.Telegram &&
                    uc.SourceId == telegramId,
                    cancellationToken);

            if (existingCredential != null)
            {
                logger.LogDebug("Credential already exists for telegram id {TelegramId}.", telegramId);
                return;
            }

            var nickname = $"@{telegramId}";

            var user = new User
            {
                Nickname = nickname
            };

            var credential = new UserCredentianl
            {
                SourceId = telegramId,
                Source = CredentialsSource.Telegram,
                User = user
            };

            await context.Users.AddAsync(user, cancellationToken);
            await context.UserCredentianls.AddAsync(credential, cancellationToken);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Registered new user {UserId} for telegram id {TelegramId}.", user.Id, telegramId);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to register for telegram id {TelegramId}.", telegramId);
            }
        }

        private async Task HandleAuthRequestMessageAsync(string? value, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                logger.LogWarning("Received AuthRequest message without payload.");
                return;
            }

            AuthRequest? payload;
            try
            {
                payload = JsonConvert.DeserializeObject<AuthRequest>(value);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize AuthRequest message: {Payload}", value);
                return;
            }

            if (payload == null)
            {
                logger.LogWarning("Deserialized AuthRequest payload is null.");
                return;
            }

            await ProcessAuthRequestAsync(payload, cancellationToken);
        }

        private async Task ProcessAuthRequestAsync(AuthRequest payload, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var kafkaMessageSender = scope.ServiceProvider.GetRequiredService<KafkaMessageSender>();

            var telegramId = payload.TelegramUserId.ToString(CultureInfo.InvariantCulture);

            var credential = await context.UserCredentianls
                .FirstOrDefaultAsync(uc =>
                    uc.Source == CredentialsSource.Telegram
                    && uc.SourceId == telegramId,
                    cancellationToken);

            if (credential == null)
            {
                logger.LogWarning("Could not find user credential for telegram id {TelegramId}.", telegramId);
                return;
            }

            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Id == credential.UserId, cancellationToken);

            if (user == null)
            {
                logger.LogWarning("Could not find user {UserId} for telegram id {TelegramId}.", credential.UserId, telegramId);
                return;
            }

            // Generate a new token
            var token = Guid.NewGuid().ToString("N");
            var validTill = DateTime.UtcNow.AddMinutes(3);

            // Find existing token for this user or create a new one
            var existingToken = await context.UserToken
                .FirstOrDefaultAsync(ut => ut.UserId == user.Id, cancellationToken);

            if (existingToken != null)
            {
                // Update existing token
                existingToken.Token = token;
                existingToken.ValidTill = validTill;
                context.UserToken.Update(existingToken);
            }
            else
            {
                // Create new token
                var userToken = new UserToken
                {
                    Token = token,
                    ValidTill = validTill,
                    UserId = user.Id,
                    User = user
                };
                await context.UserToken.AddAsync(userToken, cancellationToken);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Created/updated token for user {UserId} with telegram id {TelegramId}.", user.Id, telegramId);

                // Send AuthResponse back via Kafka
                var response = new AuthResponse
                {
                    Token = token,
                    TelegramUserId = payload.TelegramUserId
                };

                var responseJson = JsonConvert.SerializeObject(response);
                kafkaMessageSender.SendMessage(responseJson, nameof(AuthResponse));
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to create/update token for telegram id {TelegramId}.", telegramId);
            }
        }
    }
}

