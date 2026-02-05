using Microsoft.AspNetCore.WebUtilities;

namespace BMIRussian_ru.Services
{
    /// <summary>
    /// Normalizes video URLs: YouTube (embed/watch/youtu.be) to https://www.youtube.com/watch?v=VIDEO_ID;
    /// VK video_ext.php?oid=...&id=... to canonical https://vk.com/video-ownerId_id form.
    /// </summary>
    public static class VideoUrlHelper
    {
        /// <summary>Normalizes a string of video URLs (separated by ; or ,). Converts VK embed URLs to canonical watch form.</summary>
        public static string NormalizeVideoUrls(string? videoUrls)
        {
            if (string.IsNullOrWhiteSpace(videoUrls))
                return videoUrls ?? "";
            var parts = videoUrls.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var normalized = parts.Select(NormalizeOneVideoUrl).ToList();
            return string.Join("; ", normalized);
        }

        private static string NormalizeOneVideoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;
            var normalized = TryNormalizeYouTubeUrl(url);
            if (normalized != null)
                return normalized;
            if (!url.Contains("vk.com", StringComparison.OrdinalIgnoreCase) || !url.Contains("video_ext.php", StringComparison.OrdinalIgnoreCase))
                return url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
                return url;
            var query = QueryHelpers.ParseQuery(uri.Query);
            var oidStr = query.TryGetValue("oid", out var oidVals) ? oidVals.FirstOrDefault() : null;
            var idStr = query.TryGetValue("id", out var idVals) ? idVals.FirstOrDefault() : null;
            if (string.IsNullOrEmpty(oidStr) || string.IsNullOrEmpty(idStr))
                return url;
            if (!long.TryParse(oidStr, out var oid) || !long.TryParse(idStr, out var id))
                return url;
            var ownerId = Math.Abs(oid);
            return $"https://vk.com/video-{ownerId}_{id}";
        }

        /// <summary>Normalizes YouTube URLs (embed, watch, youtu.be) to canonical https://www.youtube.com/watch?v=VIDEO_ID</summary>
        private static string? TryNormalizeYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
                return null;
            var host = uri.Host ?? "";
            if (!host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) && !host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
                return null;
            string? videoId = null;
            if (host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                var seg = uri.AbsolutePath.TrimStart('/').Split('/')[0];
                if (!string.IsNullOrEmpty(seg))
                    videoId = seg;
            }
            else
            {
                var path = (uri.AbsolutePath ?? "").TrimStart('/');
                if (path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase))
                    videoId = path["embed/".Length..].Split('/')[0];
                else if (path.StartsWith("v/", StringComparison.OrdinalIgnoreCase))
                    videoId = path["v/".Length..].Split('/')[0];
                if (string.IsNullOrEmpty(videoId) && !string.IsNullOrEmpty(uri.Query))
                {
                    var query = QueryHelpers.ParseQuery(uri.Query);
                    if (query.TryGetValue("v", out var vVals))
                        videoId = vVals.FirstOrDefault();
                }
            }
            if (string.IsNullOrEmpty(videoId))
                return null;
            return $"https://www.youtube.com/watch?v={videoId}";
        }
    }
}
