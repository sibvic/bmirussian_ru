using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    public class Tag
    {
        public long Id { get; set; }

        public long VideoId { get; set; }

        [Required]
        [MaxLength(200)]
        public string TagText { get; set; } = "";

        public virtual Video Video { get; set; } = null!;
    }
}
