using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class EditChannelModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditChannelModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ChannelInput Input { get; set; } = new();

        public long ChannelId { get; set; }

        public async Task<IActionResult> OnGetAsync(long id)
        {
            var channel = await _context.Channels.FindAsync(id);
            if (channel == null)
                return NotFound();

            ChannelId = channel.Id;
            Input = new ChannelInput
            {
                Priority = channel.Priority,
                Title = channel.Title,
                ImageUrl = channel.ImageUrl,
                Description = channel.Description
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(long id)
        {
            var channel = await _context.Channels.FindAsync(id);
            if (channel == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ChannelId = id;
                return Page();
            }

            channel.Priority = Input.Priority;
            channel.Title = Input.Title;
            channel.SEOId = SeoIdHelper.FromTitle(Input.Title);
            channel.ImageUrl = Input.ImageUrl;
            channel.Description = Input.Description;

            await _context.SaveChangesAsync();
            return RedirectToPage("/Admin/Channels");
        }
    }
}
