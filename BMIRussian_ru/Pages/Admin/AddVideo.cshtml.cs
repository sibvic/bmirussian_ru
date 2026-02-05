using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class AddVideoModel(ApplicationDbContext context) : PageModel
    {
        [BindProperty]
        public EditVideoInput Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Input.PublishDate = DateTime.UtcNow.Date;
            Input.Status = VideoStatus.Editing;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var video = new Video
            {
                Title = Input.Title,
                SEOId = SeoIdHelper.FromTitle(Input.Title),
                Description = Input.Description,
                ImageUrl = Input.ImageUrl,
                PublishDate = DateTime.SpecifyKind(Input.PublishDate, DateTimeKind.Utc),
                VideoUrls = VideoUrlHelper.NormalizeVideoUrls(Input.VideoUrls),
                Status = Input.Status,
                Keywords = Input.Keywords ?? ""
            };
            context.Videos.Add(video);
            await context.SaveChangesAsync();

            var tagTexts = (Input.Tags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .Where(s => s.Length > 0)
                .Distinct();
            foreach (var tagText in tagTexts)
            {
                context.Tags.Add(new Tag { VideoId = video.Id, TagText = tagText });
            }
            if (tagTexts.Any())
                await context.SaveChangesAsync();

            return RedirectToPage("/Admin/Video");
        }
    }
}
