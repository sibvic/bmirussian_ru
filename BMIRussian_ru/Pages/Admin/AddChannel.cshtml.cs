using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages.Admin
{
    public class AddChannelModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AddChannelModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ChannelInput Input { get; set; } = new();

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var channel = new Channel
            {
                Priority = Input.Priority,
                Title = Input.Title,
                ImageUrl = Input.ImageUrl,
                Description = Input.Description
            };
            _context.Channels.Add(channel);
            await _context.SaveChangesAsync();
            return RedirectToPage("/Admin/Channels");
        }
    }

    public class ChannelInput
    {
        [Display(Name = "Приоритет")]
        public int Priority { get; set; }

        [Required(ErrorMessage = "Укажите название")]
        [Display(Name = "Название")]
        public string Title { get; set; } = "";

        [Display(Name = "Ссылка на миниатюру")]
        [DataType(DataType.Url)]
        public string? ImageUrl { get; set; }

        [Display(Name = "Описание")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }
    }
}
