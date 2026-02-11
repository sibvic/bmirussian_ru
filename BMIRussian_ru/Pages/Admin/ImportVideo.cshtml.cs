using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Admin
{
    public class ImportVideoModel(ApplicationDbContext context, IMediaInfoKafkaService mediaInfoKafkaService) : PageModel
    {
        [BindProperty]
        [Display(Name = "Данные")]
        public string Data { get; set; } = "";

        public string? Message { get; set; }
        public bool IsError { get; set; }

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            // Import by URL (YouTube / VK): one URL per line
            var (imported, errors) = await ImportByUrlsAsync(Data);
            await context.SaveChangesAsync();
            if (imported > 0)
            {
                if (errors.Count == 0)
                    return RedirectToPage("/Admin/Video");
            }

            ModelState.AddModelError(nameof(Data), "Введите данные для импорта или URL-ы видео.");
            return Page();
        }

        private async Task<(int imported, List<string> errors)> ImportByUrlsAsync(string urlsText)
        {
            var errors = new List<string>();
            var imported = 0;
            var urls = urlsText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            if (urls.Count == 0)
            {
                errors.Add("Не найдено ни одного URL (http/https).");
                return (0, errors);
            }

            foreach (var url in urls)
            {
                if (!IsYouTubeUrl(url) && !IsVkUrl(url))
                {
                    errors.Add($"Неподдерживаемый URL (только YouTube и VK): {url}");
                    continue;
                }

                VideoMetadata? meta;
                try
                {
                    meta = await GetVideoMetadataAsync(url);
                }
                catch (Exception ex)
                {
                    errors.Add($"{url}: {ex.Message}");
                    continue;
                }

                if (meta == null)
                {
                    errors.Add($"{url}: не удалось получить метаданные.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(meta.Title))
                {
                    errors.Add($"Не удалось получить название: {url}");
                    continue;
                }

                var imageUrl = meta.Thumbnail;
                // YouTube: build thumbnail if not from yt-dlp
                if (imageUrl == null && TryGetYouTubeVideoId(url, out var ytId))
                    imageUrl = $"https://i.ytimg.com/vi/{ytId}/maxresdefault.jpg";

                var video = new Video
                {
                    Title = meta.Title,
                    SEOId = SeoIdHelper.FromTitle(meta.Title),
                    Description = meta.Description ?? "",
                    ImageUrl = imageUrl ?? "",
                    PublishDate = meta.PublishDate,
                    VideoUrls = url,
                    Status = VideoStatus.Editing,
                    Keywords = ""
                };
                context.Videos.Add(video);
                imported++;
            }

            return (imported, errors);
        }

        private static bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                   url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVkUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return (url.Contains("vk.com/", StringComparison.OrdinalIgnoreCase) || url.Contains("vkvideo.ru/", StringComparison.OrdinalIgnoreCase))
                   && url.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryGetYouTubeVideoId(string url, out string? videoId)
        {
            videoId = null;
            var m = Regex.Match(url, @"(?:youtube\.com/watch\?.*v=|youtu\.be/)([a-zA-Z0-9_-]{11})");
            if (m.Success)
                videoId = m.Groups[1].Value;
            return videoId != null;
        }

        private static string BuildYouTubeThumbnailUrl(string videoId) =>
            $"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg";

        private sealed record VideoMetadata(string? Title, string? Thumbnail, string? Description, DateTime PublishDate);

        /// <summary>
        /// Gets video metadata via Kafka MediaInfo message when configured; otherwise via local yt-dlp.
        /// </summary>
        private async Task<VideoMetadata?> GetVideoMetadataAsync(string url)
        {
            var kafkaResult = await mediaInfoKafkaService.GetVideoMetadataAsync(url);
            if (kafkaResult != null)
            {
                if (!kafkaResult.IsSuccess)
                {
                    throw new InvalidOperationException(kafkaResult.Error ?? "Unknown error");
                }
                return new VideoMetadata(
                    kafkaResult.Title,
                    kafkaResult.Thumbnail,
                    kafkaResult.Description,
                    kafkaResult.PublishDate);
            }
            return await GetVideoMetadataViaYtDlpAsync(url);
        }

        private static async Task<VideoMetadata> GetVideoMetadataViaYtDlpAsync(string url)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                ArgumentList = { "--no-download", "-j", "-q", url },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Не удалось запустить yt-dlp. Установите yt-dlp и добавьте его в PATH.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "yt-dlp вернул ошибку." : stderr.Trim());

            if (string.IsNullOrWhiteSpace(stdout))
                throw new InvalidOperationException("yt-dlp не вернул данные.");

            var json = JObject.Parse(stdout);
            var title = json["title"]?.ToString();
            var thumbnail = json["thumbnail"]?.ToString();
            var description = json["description"]?.ToString();

            if (string.IsNullOrWhiteSpace(thumbnail) && TryGetYouTubeVideoId(url, out var ytId))
                thumbnail = BuildYouTubeThumbnailUrl(ytId);

            var publishDate = ParsePublishDate(json);

            return new VideoMetadata(title, thumbnail, description, publishDate);
        }

        private static DateTime ParsePublishDate(JObject json)
        {
            // upload_date: YYYYMMDD
            var uploadDate = json["upload_date"]?.ToString();
            if (!string.IsNullOrEmpty(uploadDate) && uploadDate.Length == 8 &&
                DateTime.TryParseExact(uploadDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

            // timestamp: Unix seconds
            var ts = json["timestamp"];
            if (ts != null && ts.Type == JTokenType.Integer && long.TryParse(ts.ToString(), out var unixSeconds))
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

            return DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        }
    }
}
