using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    /// <summary>STL generation job (3D shop): queued request, result object name or error.</summary>
    public class StlJob
    {
        public long Id { get; set; }

        /// <summary>Correlation id used in Kafka messages and the MinIO object name (shop/{RequestId}.stl).</summary>
        [Required]
        [MaxLength(64)]
        public string RequestId { get; set; } = "";

        public long UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Model { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string Text { get; set; } = "";

        public StlJobStatus Status { get; set; } = StlJobStatus.Queued;

        /// <summary>MinIO object name of the generated STL (set on Done).</summary>
        [MaxLength(200)]
        public string? ObjectName { get; set; }

        [MaxLength(500)]
        public string? Error { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}
