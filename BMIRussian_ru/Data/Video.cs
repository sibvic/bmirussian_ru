using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    public class Video
    {
        public long Id { get; set; }

        [Required]
        public string Title { get; set; }

        /// <summary>Normalized title for URLs (transliteration, uppercase, spaces→hyphens). Filled automatically.</summary>
        public string? SEOId { get; set; }

        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [DataType(DataType.ImageUrl)]
        public string? ImageUrl { get; set; }

        [DataType(DataType.Date)]
        public DateTime PublishDate { get; set; }

        [Required]
        public string VideoUrls { get; set; }

        public VideoStatus Status { get; set; }

        public long ChannelId { get; set; }

        public virtual Channel Channel { get; set; }

        public string Keywords { get; set; }

        public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
    }
}
