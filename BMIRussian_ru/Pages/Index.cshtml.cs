using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Pages
{
    public class IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context) : PageModel
    {
        public IList<Video> LatestVideos { get; set; } = new List<Video>();
        public Video? FeaturedVideo { get; set; }
        public IEnumerable<Video> GridVideos =>
            FeaturedVideo == null ? LatestVideos : LatestVideos.Where(v => v.Id != FeaturedVideo.Id);

        public async Task OnGetAsync()
        {
            LatestVideos = await context.Videos
                .Include(v => v.Tags)
                .Where(v => v.Status == VideoStatus.Published)
                .OrderByDescending(v => v.PublishDate)
                .Take(9)
                .ToListAsync();

            FeaturedVideo = LatestVideos.FirstOrDefault(v => !string.IsNullOrEmpty(v.ImageUrl))
                ?? LatestVideos.FirstOrDefault();
        }
    }
}
