using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    public class Channel
    {
        public long Id { get; set; }

        public int Priority { get; set; }

        [Required]
        [Display(Name = "Название")]
        public string Title { get; set; }

        [DataType(DataType.ImageUrl)]
        [Display(Name = "Ссылка на миниатюру")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Описание")]
        public string? Description { get; set; }
    }
}
