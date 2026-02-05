using Microsoft.AspNetCore.WebUtilities;

namespace BMIRussian_ru.Services
{
    /// <summary>
    /// Normalizes video URLs, e.g. converts VK video_ext.php?oid=...&id=... to canonical https://vk.com/video-ownerId_id form.
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
    }
}
