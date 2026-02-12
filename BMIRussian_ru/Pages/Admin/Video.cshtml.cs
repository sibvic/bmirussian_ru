using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class VideoModel(ApplicationDbContext context, IMeilisearchService meilisearch) : PageModel
    {
        public const int PageSize = 20;

        public IList<Video> Videos { get; set; } = new List<Video>();
        public int PageIndex { get; set; } = 1;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
        public VideoStatus? StatusFilter { get; set; }
        public string? TitleFilter { get; set; }
        public bool NoDescriptionFilter { get; set; }
        public bool NoTranscriptFilter { get; set; }

        private const string StatusFilterKey = "Admin.Video.StatusFilter";

        public async Task OnGetAsync(int pageIndex = 1, VideoStatus? statusFilter = null, string? titleFilter = null, bool noDescriptionFilter = false, bool noTranscriptFilter = false, bool clearFilter = false)
        {
            PageIndex = Math.Max(1, pageIndex);

            if (clearFilter)
            {
                Response.Cookies.Delete(StatusFilterKey, new CookieOptions { Path = "/" });
                StatusFilter = null;
                TitleFilter = null;
                NoDescriptionFilter = false;
                NoTranscriptFilter = false;
            }
            else
            {
                TitleFilter = titleFilter;
                if (Request.Query.ContainsKey("statusFilter"))
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
                NoDescriptionFilter = noDescriptionFilter;
                NoTranscriptFilter = noTranscriptFilter;
            }

            IQueryable<Video> query = context.Videos
                .OrderByDescending(v => v.PublishDate);

            if (StatusFilter.HasValue)
                query = query.Where(v => v.Status == StatusFilter.Value);

            if (!string.IsNullOrWhiteSpace(TitleFilter))
            {
                var titleLower = TitleFilter.Trim().ToLower();
                query = query.Where(v => v.Title != null && v.Title.ToLower().Contains(titleLower));
            }

            if (NoDescriptionFilter)
                query = query.Where(v => string.IsNullOrWhiteSpace(v.Description));

            if (NoTranscriptFilter)
                query = query.Where(v => !v.HasTranscript);

            TotalCount = await query.CountAsync();

            Videos = await query
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            var video = await context.Videos.FindAsync(id);
            if (video != null)
            {
                context.Videos.Remove(video);
                await context.SaveChangesAsync();
                await meilisearch.DeleteVideoAsync(id);
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostIndexAllAsync()
        {
            var videos = await context.Videos.Where(v => v.Status == VideoStatus.Published).ToListAsync();
            await meilisearch.IndexVideosAsync(videos);
            TempData["IndexAllMessage"] = $"Проиндексировано видео: {videos.Count}.";
            return RedirectToPage();
        }
    }
}
