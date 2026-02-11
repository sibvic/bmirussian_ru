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
            if (imported > 0 && errors.Count == 0)
                return RedirectToPage("/Admin/Video");

            if (string.IsNullOrWhiteSpace(Data))
            {
                ModelState.AddModelError(nameof(Data), "Введите данные для импорта или URL-ы видео.");
                return Page();
            }

            var rows = ParseTuples(Data);
            if (rows.Count == 0)
            {
                ModelState.AddModelError(nameof(Data), "Не удалось распознать ни одной строки данных. Формат: ('SeoId', 'ChannelSeoId', 'Description', 'ImageUrl', 'Date', N, 'Title', 'videoUrl', ...)");
                return Page();
            }

            foreach (var values in rows)
            {
                if (values.Count < 8)
                {
                    errors.Add($"Строка с недостаточным количеством полей: {values.Count}");
                    continue;
                }

                var videoSeoId = values[0]?.Trim().Trim('\'') ?? "";
                var description = Regex.Replace(values[2]?.Trim().Trim('\'') ?? "", @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                var imageUrl = values[3]?.Trim().Trim('\'') ?? "";
                var dateStr = values[4]?.Trim().Trim('\'') ?? "";
                var title = values[6]?.Trim().Trim('\'') ?? "";
                var videoUrl = values[7]?.Trim().Trim('\'') ?? "";

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(videoUrl))
                {
                    errors.Add($"Пропущены название или URL: '{title}' / '{videoUrl}'");
                    continue;
                }
                if (!DateTime.TryParse(dateStr, out var publishDate))
                    publishDate = DateTime.UtcNow.Date;

                var video = new Video
                {
                    Title = title,
                    SEOId = string.IsNullOrEmpty(videoSeoId) ? SeoIdHelper.FromTitle(title) : videoSeoId,
                    Description = description,
                    ImageUrl = imageUrl,
                    PublishDate = DateTime.SpecifyKind(publishDate, DateTimeKind.Utc),
                    VideoUrls = videoUrl,
                    Status = VideoStatus.Editing,
                    Keywords = ""
                };
                context.Videos.Add(video);
                imported++;
            }

            await context.SaveChangesAsync();

            if (imported > 0 && errors.Count == 0)
                return RedirectToPage("/Admin/Video");

            Message = imported > 0
                ? $"Импортировано видео: {imported}." + (errors.Count > 0 ? " Ошибки: " + string.Join("; ", errors) : "")
                : "Ошибки: " + string.Join("; ", errors);
            IsError = errors.Count > 0;
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

        private static List<List<string>> ParseTuples(string text)
        {
            var result = new List<List<string>>();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '(') continue;
                var start = i + 1;
                var inString = false;
                var j = start;
                while (j < text.Length)
                {
                    var c = text[j];
                    if (inString)
                    {
                        if (c == '\'' && (j + 1 >= text.Length || text[j + 1] != '\''))
                            inString = false;
                        else if (c == '\'' && j + 1 < text.Length && text[j + 1] == '\'')
                            j++;
                    }
                    else if (c == '\'')
                        inString = true;
                    else if (c == ')')
                        break;
                    j++;
                }
                if (j < text.Length)
                {
                    var inner = text[start..j];
                    var values = ParseValues(inner);
                    if (values.Count > 0)
                        result.Add(values);
                }
                i = j;
            }
            return result;
        }

        private static List<string> ParseValues(string tupleContent)
        {
            var result = new List<string>();
            var i = 0;
            while (i < tupleContent.Length)
            {
                while (i < tupleContent.Length && (tupleContent[i] == ',' || char.IsWhiteSpace(tupleContent[i])))
                    i++;
                if (i >= tupleContent.Length) break;

                if (tupleContent[i] == '\'')
                {
                    var start = i + 1;
                    i++;
                    while (i < tupleContent.Length)
                    {
                        if (tupleContent[i] == '\'' && (i + 1 >= tupleContent.Length || tupleContent[i + 1] != '\''))
                            break;
                        if (tupleContent[i] == '\'' && i + 1 < tupleContent.Length && tupleContent[i + 1] == '\'')
                            i++;
                        i++;
                    }
                    result.Add(tupleContent[start..i].Replace("''", "'"));
                    i++;
                }
                else if (char.IsDigit(tupleContent[i]) || (tupleContent[i] == '-' && i + 1 < tupleContent.Length && char.IsDigit(tupleContent[i + 1])))
                {
                    var start = i;
                    if (tupleContent[i] == '-') i++;
                    while (i < tupleContent.Length && (char.IsDigit(tupleContent[i]) || tupleContent[i] == '.'))
                        i++;
                    result.Add(tupleContent[start..i].Trim());
                }
                else if (i + 4 <= tupleContent.Length && tupleContent.AsSpan(i, 4).Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add("");
                    i += 4;
                }
                else
                {
                    var start = i;
                    while (i < tupleContent.Length && tupleContent[i] != ',')
                        i++;
                    result.Add(tupleContent[start..i].Trim());
                }
            }
            return result;
        }
    }
}
