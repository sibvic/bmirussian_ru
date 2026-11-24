using System.Text.RegularExpressions;
using Markdig;
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
        public string? FeaturedSummary => Summarize(FeaturedVideo?.Description, 220);
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

        public static string? Summarize(string? markdown, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return null;

            var text = Regex.Replace(Markdown.ToPlainText(markdown), @"</?[a-zA-Z][a-zA-Z0-9-]*(\s[^<>]*)?/?>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length <= maxLength)
                return text;

            var cut = text.LastIndexOf(' ', maxLength);
            return text[..(cut > 0 ? cut : maxLength)].TrimEnd(' ', ',', '.', ';', ':', '—', '-') + "…";
        }
    }
}
