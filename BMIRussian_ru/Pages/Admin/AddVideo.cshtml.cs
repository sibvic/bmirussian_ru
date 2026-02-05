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
        public List<SelectListItem> Channels { get; set; } = new();

        [BindProperty]
        public EditVideoInput Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadChannelsAsync();
            if (Channels.Count > 0 && Input.ChannelId == 0)
            {
                var firstChannel = await context.Channels
                    .OrderBy(c => c.Priority)
                    .ThenBy(c => c.Title)
                    .FirstOrDefaultAsync();
                if (firstChannel != null)
                    Input.ChannelId = firstChannel.Id;
            }
            Input.PublishDate = DateTime.UtcNow.Date;
            Input.Status = VideoStatus.Editing;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadChannelsAsync();
                return Page();
            }

            var video = new Video
            {
                Title = Input.Title,
                SEOId = SeoIdHelper.FromTitle(Input.Title),
                Description = Input.Description,
                ImageUrl = Input.ImageUrl,
                PublishDate = DateTime.SpecifyKind(Input.PublishDate, DateTimeKind.Utc),
                VideoUrls = Input.VideoUrls,
                Status = Input.Status,
                ChannelId = Input.ChannelId,
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

        private async Task LoadChannelsAsync()
        {
            Channels = await context.Channels
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.Title)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString()))
                .ToListAsync();
        }
    }
}
