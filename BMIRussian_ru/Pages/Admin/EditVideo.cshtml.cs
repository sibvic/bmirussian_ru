using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages.Admin
{
    public class EditVideoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditVideoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SelectListItem> Channels { get; set; } = new();

        [BindProperty]
        public EditVideoInput Input { get; set; } = new();

        public long VideoId { get; set; }

        public async Task<IActionResult> OnGetAsync(long id)
        {
            var video = await _context.Videos
                .Include(v => v.Channel)
                .FirstOrDefaultAsync(v => v.Id == id);
            if (video == null)
                return NotFound();

            VideoId = video.Id;
            Input = new EditVideoInput
            {
                Title = video.Title,
                Description = video.Description,
                ImageUrl = video.ImageUrl,
                PublishDate = video.PublishDate,
                VideoUrls = video.VideoUrls,
                Status = video.Status,
                ChannelId = video.ChannelId,
                Keywords = video.Keywords ?? ""
            };

            await LoadChannelsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(long id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                VideoId = id;
                await LoadChannelsAsync();
                return Page();
            }

            video.Title = Input.Title;
            video.Description = Input.Description;
            video.ImageUrl = Input.ImageUrl;
            video.PublishDate = Input.PublishDate;
            video.VideoUrls = Input.VideoUrls;
            video.Status = Input.Status;
            video.ChannelId = Input.ChannelId;
            video.Keywords = Input.Keywords ?? "";

            await _context.SaveChangesAsync();
            return RedirectToPage("/Admin/Video");
        }

        private async Task LoadChannelsAsync()
        {
            Channels = await _context.Channels
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.Title)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString()))
                .ToListAsync();
        }
    }

    public class EditVideoInput
    {
        [Required(ErrorMessage = "Укажите название")]
        [Display(Name = "Название")]
        public string Title { get; set; } = "";

        [Display(Name = "Описание")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Display(Name = "Ссылка на изображение")]
        [DataType(DataType.Url)]
        public string? ImageUrl { get; set; }

        [Required]
        [Display(Name = "Дата публикации")]
        [DataType(DataType.Date)]
        public DateTime PublishDate { get; set; }

        [Required(ErrorMessage = "Укажите ссылку на видео")]
        [Display(Name = "Ссылки на видео (через ; или запятую)")]
        public string VideoUrls { get; set; } = "";

        [Display(Name = "Статус")]
        public VideoStatus Status { get; set; }

        [Display(Name = "Канал")]
        public long ChannelId { get; set; }

        [Display(Name = "Ключевые слова")]
        public string Keywords { get; set; } = "";
    }
}
