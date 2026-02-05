using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages.Admin
{
    public class VideoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public VideoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public const int PageSize = 20;

        public IList<Video> Videos { get; set; } = new List<Video>();
        public int PageIndex { get; set; } = 1;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
        public VideoStatus? StatusFilter { get; set; }

        public async Task OnGetAsync(int pageIndex = 1, VideoStatus? statusFilter = null)
        {
            PageIndex = Math.Max(1, pageIndex);
            StatusFilter = statusFilter;

            IQueryable<Video> query = _context.Videos
                .Include(v => v.Channel)
                .OrderByDescending(v => v.PublishDate);

            if (StatusFilter.HasValue)
                query = query.Where(v => v.Status == StatusFilter.Value);

            TotalCount = await query.CountAsync();

            Videos = await query
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video != null)
            {
                _context.Videos.Remove(video);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
