using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class EditVideoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditVideoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public EditVideoInput Input { get; set; } = new();

        public long VideoId { get; set; }

        public async Task<IActionResult> OnGetAsync(long id)
        {
            var video = await _context.Videos
                .Include(v => v.Tags)
                .FirstOrDefaultAsync(v => v.Id == id);
            if (video == null)
                return NotFound();

            VideoId = video.Id;
            var tagsString = video.Tags.Count > 0
                ? string.Join(", ", video.Tags.OrderBy(t => t.TagText).Select(t => t.TagText))
                : "";
            Input = new EditVideoInput
            {
                Title = video.Title,
                Description = video.Description,
                ImageUrl = video.ImageUrl,
                PublishDate = video.PublishDate,
                VideoUrls = VideoUrlHelper.NormalizeVideoUrls(video.VideoUrls),
                Status = video.Status,
                Keywords = video.Keywords ?? "",
                Tags = tagsString
            };

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
                return Page();
            }

            video.Title = Input.Title;
            video.SEOId = SeoIdHelper.FromTitle(Input.Title);
            video.Description = Input.Description;
            video.ImageUrl = Input.ImageUrl;
            video.PublishDate = DateTime.SpecifyKind(Input.PublishDate, DateTimeKind.Utc);
            video.VideoUrls = VideoUrlHelper.NormalizeVideoUrls(Input.VideoUrls);
            video.Status = Input.Status;
            video.Keywords = Input.Keywords ?? "";

            var newTagSet = (Input.Tags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToHashSet();

            var existingTags = await _context.Tags.Where(t => t.VideoId == id).ToListAsync();
            var toRemove = existingTags.Where(t => !newTagSet.Contains(t.TagText)).ToList();
            var existingTexts = existingTags.Select(t => t.TagText).ToHashSet();
            var toAdd = newTagSet.Where(t => !existingTexts.Contains(t)).ToList();

            _context.Tags.RemoveRange(toRemove);
            foreach (var tagText in toAdd)
            {
                _context.Tags.Add(new Tag { VideoId = id, TagText = tagText });
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("/Admin/Video");
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

        [Display(Name = "Ключевые слова")]
        public string? Keywords { get; set; } = "";

        [Display(Name = "Теги (через запятую)")]
        public string? Tags { get; set; } = "";
    }
}
