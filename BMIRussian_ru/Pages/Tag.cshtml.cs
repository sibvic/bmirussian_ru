using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using Microsoft.AspNetCore.Mvc;

namespace BMIRussian_ru.Pages
{
    public class TagModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TagModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public const int PageSize = 15;

        /// <summary>Normalized tag (lowercase) used for the query.</summary>
        public string TagName { get; set; } = "";

        public IList<Video> Videos { get; set; } = new List<Video>();
        public int PageIndex { get; set; } = 1;
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public async Task<IActionResult> OnGetAsync(string? tag, int pageIndex = 1)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return NotFound();

            TagName = tag.Trim().ToLowerInvariant();
            PageIndex = Math.Max(1, pageIndex);

            var query = _context.Videos
                .Include(v => v.Channel)
                .Include(v => v.Tags)
                .Where(v => v.Status == VideoStatus.Published && v.Tags.Any(t => t.TagText == TagName))
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
