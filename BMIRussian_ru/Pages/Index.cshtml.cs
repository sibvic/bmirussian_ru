using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IList<Video> LatestVideos { get; set; } = new List<Video>();

        public async Task OnGetAsync()
        {
            LatestVideos = await _context.Videos
                .Include(v => v.Channel)
                .OrderByDescending(v => v.PublishDate)
                .Take(10)
                .ToListAsync();
        }
    }
}