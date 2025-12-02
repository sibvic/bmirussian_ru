using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    public enum VideoStatus
    {
        [Display(Name = "Черновик")]
        Editing,
        [Display(Name = "Запланирован")]
        Scheduled,
        [Display(Name = "Опубликован")]
        Published
    }
}
