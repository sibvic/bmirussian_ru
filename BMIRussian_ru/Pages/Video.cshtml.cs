using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BMIRussian_ru.Data;
using Sibvic.AuthLib;

namespace BMIRussian_ru.Pages
{
    public class VideoModel(ApplicationDbContext context) : PageModel
    {
        public Video? Video { get; set; }

        /// <summary>First video URL from VideoUrls (for "watch" link when not embeddable).</summary>
        public string? FirstVideoUrl { get; set; }

        /// <summary>Embed URL for YouTube or VK when first URL is embeddable; otherwise null.</summary>
        public string? EmbedUrl { get; set; }

        public bool IsAdmin { get; set; }

        public async Task<IActionResult> OnGetAsync(string? seoId)
        {
            if (string.IsNullOrEmpty(seoId))
                return NotFound();

            var seoIdLower = seoId.ToLowerInvariant();
            var video = await context.Videos
                .Include(v => v.Tags)
                .FirstOrDefaultAsync(v => v.SEOId != null && v.SEOId.ToLower() == seoIdLower && v.Status == VideoStatus.Published)
                ?? (long.TryParse(seoId, out var id) ? await context.Videos.Include(v => v.Tags).FirstOrDefaultAsync(v => v.Id == id && v.Status == VideoStatus.Published) : null);

            if (video == null)
                return NotFound();

            Video = video;

            var urls = video.VideoUrls?
                .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            FirstVideoUrl = urls?.FirstOrDefault();

            if (!string.IsNullOrEmpty(FirstVideoUrl))
            {
                if (FirstVideoUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                    || FirstVideoUrl.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(FirstVideoUrl);
                    var videoId = uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
                        ? uri.AbsolutePath.TrimStart('/').Split('/').FirstOrDefault()
                        : GetQueryValue(uri.Query, "v");
                    if (!string.IsNullOrEmpty(videoId))
                        EmbedUrl = $"https://www.youtube.com/embed/{videoId}";
                }
                else if (FirstVideoUrl.Contains("vk.com", StringComparison.OrdinalIgnoreCase))
                {
                    var vkEmbed = TryGetVkEmbedUrl(FirstVideoUrl);
                    if (vkEmbed != null)
                        EmbedUrl = vkEmbed;
                }
            }

            if (HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? HttpContext.User.FindFirstValue("id");
                if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var userId))
                    IsAdmin = await context.Set<UserRoles>().AnyAsync(r => r.UserId == userId && r.Role == "Admin");
            }

            return Page();
        }

        /// <summary>Parses VK watch URL into video_ext.php embed URL (e.g. video-45671298_456241811 → oid=-45671298, id=456241811).</summary>
        private static string? TryGetVkEmbedUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host == null)
                return null;
            if (!uri.Host.Contains("vk.com", StringComparison.OrdinalIgnoreCase))
                return null;

            // Path: /video-45671298_456241811 or /video45671298_456241811
            var path = uri.AbsolutePath.TrimStart('/');
            var videoSegment = path.StartsWith("video", StringComparison.OrdinalIgnoreCase)
                ? path
                : GetQueryValue(uri.Query, "z"); // z=video-45671298_456241811

            if (string.IsNullOrEmpty(videoSegment) || !videoSegment.StartsWith("video", StringComparison.OrdinalIgnoreCase))
                return null;

            // video-45671298_456241811 or video45671298_456241811
            var rest = videoSegment.Length > 5 ? videoSegment[5..] : null; // "-45671298_456241811" or "45671298_456241811"
            if (string.IsNullOrEmpty(rest))
                return null;

            string oidStr;
            if (rest[0] == '-')
            {
                var under = rest.IndexOf('_');
                if (under <= 1) return null;
                oidStr = rest[..under]; // -45671298
                rest = rest[(under + 1)..];
            }
            else
            {
                var under = rest.IndexOf('_');
                if (under < 1) return null;
                oidStr = rest[..under];
                rest = rest[(under + 1)..];
            }

            if (string.IsNullOrEmpty(rest) || !long.TryParse(oidStr, out var oid) || !long.TryParse(rest, out var videoId))
                return null;

            return $"https://vk.com/video_ext.php?oid={oid}&id={videoId}";
        }

        private static string? GetQueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query) || query[0] == '?')
                query = query.Length > 1 ? query[1..] : "";
            foreach (var pair in query.Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0 && string.Equals(pair[..eq].Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(pair[(eq + 1)..].Trim());
            }
            return null;
        }
    }
}
