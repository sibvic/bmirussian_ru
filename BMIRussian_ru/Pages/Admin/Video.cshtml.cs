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

        private const string StatusFilterKey = "Admin.Video.StatusFilter";

        public async Task OnGetAsync(int pageIndex = 1, VideoStatus? statusFilter = null, bool clearFilter = false)
        {
            PageIndex = Math.Max(1, pageIndex);

            if (clearFilter)
            {
                Response.Cookies.Delete(StatusFilterKey, new CookieOptions { Path = "/" });
                StatusFilter = null;
            }
            else if (Request.Query.ContainsKey("statusFilter"))
            {
                StatusFilter = statusFilter;
                if (statusFilter.HasValue)
                    Response.Cookies.Append(StatusFilterKey, ((int)statusFilter.Value).ToString(), new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(30) });
                else
                    Response.Cookies.Delete(StatusFilterKey, new CookieOptions { Path = "/" });
            }
            else
            {
                if (Request.Cookies.TryGetValue(StatusFilterKey, out var cookieVal) && int.TryParse(cookieVal, out var saved) && Enum.IsDefined(typeof(VideoStatus), saved))
                    StatusFilter = (VideoStatus)saved;
                else
                    StatusFilter = null;
            }

            IQueryable<Video> query = _context.Videos
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
