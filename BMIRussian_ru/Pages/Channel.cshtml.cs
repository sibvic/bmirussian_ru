using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages
{
    public class ChannelModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ChannelModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public const int PageSize = 15;

        public Channel? Channel { get; set; }
        public IList<Video> Videos { get; set; } = new List<Video>();
        public int PageIndex { get; set; } = 1;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public async Task<IActionResult> OnGetAsync(string seoId, int pageIndex = 1)
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.SEOId != null && c.SEOId.Equals(seoId, StringComparison.OrdinalIgnoreCase))
                ?? (long.TryParse(seoId, out var id) ? await _context.Channels.FindAsync(id) : null);
            if (channel == null)
                return NotFound();

            Channel = channel;
            PageIndex = Math.Max(1, pageIndex);

            var query = _context.Videos
                .Include(v => v.Channel)
                .Where(v => v.ChannelId == channel.Id && v.Status == VideoStatus.Published)
                .OrderByDescending(v => v.PublishDate);

            TotalCount = await query.CountAsync();

            Videos = await query
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }
    }
}
