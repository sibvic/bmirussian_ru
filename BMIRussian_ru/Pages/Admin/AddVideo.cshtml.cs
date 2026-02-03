using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages.Admin
{
    public class AddVideoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AddVideoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SelectListItem> Channels { get; set; } = new();

        [BindProperty]
        public EditVideoInput Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadChannelsAsync();
            if (Channels.Count > 0 && Input.ChannelId == 0)
            {
                var firstChannel = await _context.Channels
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
                Description = Input.Description,
                ImageUrl = Input.ImageUrl,
                PublishDate = Input.PublishDate,
                VideoUrls = Input.VideoUrls,
                Status = Input.Status,
                ChannelId = Input.ChannelId,
                Keywords = Input.Keywords ?? ""
            };
            _context.Videos.Add(video);
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
}
