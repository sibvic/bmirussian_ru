using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages
{
    public class VideosModel(ApplicationDbContext context, IMeilisearchService meilisearch) : PageModel
    {
        public const int PageSize = 15;

        public IList<Video> Videos { get; set; } = new List<Video>();
        public int PageIndex { get; set; } = 1;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
        public string? SearchQuery { get; set; }

        public async Task OnGetAsync(int pageIndex = 1, string? q = null)
        {
            PageIndex = Math.Max(1, pageIndex);
            SearchQuery = q?.Trim();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var searchResult = await meilisearch.SearchVideosAsync(SearchQuery, PageSize, (PageIndex - 1) * PageSize);
                TotalCount = searchResult.EstimatedTotalHits;

                if (searchResult.VideoIds.Count > 0)
                {
                    var ids = searchResult.VideoIds.ToList();
                    var videosById = await context.Videos
                        .Include(v => v.Tags)
                        .Where(v => ids.Contains(v.Id) && v.Status == VideoStatus.Published)
                        .ToDictionaryAsync(v => v.Id);

                    Videos = ids
                        .Where(id => videosById.ContainsKey(id))
                        .Select(id => videosById[id])
                        .ToList();
                }
            }
            else
            {
                var query = context.Videos
                    .Include(v => v.Tags)
                    .Where(v => v.Status == VideoStatus.Published)
                    .OrderByDescending(v => v.PublishDate);

                TotalCount = await query.CountAsync();

                Videos = await query
                    .Skip((PageIndex - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
            }
        }
    }
}
