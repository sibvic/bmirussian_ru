using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    public class Video
    {
        public string ID { get; set; }

        public string MainID { get; set; }

        [Required]
        public string Title { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [DataType(DataType.ImageUrl)]
        public string ImageUrl { get; set; }

        [DataType(DataType.Date)]
        public DateTime PublishDate { get; set; }

        [Required]
        public string VideoUrls { get; set; }

        public VideoStatus Status { get; set; }

        public string ChannelId { get; set; }

        public virtual Channel Channel { get; set; }

        public string Keywords { get; set; }
    }
}
