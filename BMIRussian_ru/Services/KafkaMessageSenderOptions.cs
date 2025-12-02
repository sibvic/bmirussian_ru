namespace BMIRussian_ru.Services
{
    public record KafkaMessageSenderOptions
    {
        public string BootstrapServers { get; init; } = string.Empty;
        public string InTopic { get; init; } = string.Empty;
        public string OutTopic { get; init; } = string.Empty;
        public string? ClientId { get; init; }
        public int? MessageTimeoutMs { get; init; }
        public int? SocketTimeoutMs { get; init; }
        public IReadOnlyDictionary<string, string>? AdditionalConfig { get; init; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

