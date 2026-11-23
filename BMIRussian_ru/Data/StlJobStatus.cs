using System.ComponentModel.DataAnnotations;

namespace BMIRussian_ru.Data
{
    public enum StlJobStatus
    {
        [Display(Name = "В очереди")]
        Queued,
        [Display(Name = "Готово")]
        Done,
        [Display(Name = "Ошибка")]
        Failed
    }
}
