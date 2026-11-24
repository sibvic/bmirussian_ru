using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Services;

/// <summary>
/// Imports videos into the database: from pasted admin URLs (ImportVideo page)
/// and from Telegram channel posts (VideoImportConsumerService).
/// Metadata is fetched via Kafka MediaInfo when configured, otherwise via local yt-dlp.
/// </summary>
public class VideoImportService(
    ApplicationDbContext context,
    IMediaInfoKafkaService mediaInfoKafkaService,
    IConfiguration configuration,
    ILogger<VideoImportService> logger,
    MetricsService metrics)
{
    public sealed record TelegramImportResult(Video? Video, bool Duplicate, string? Error);

    /// <summary>
    /// Imports a video posted to the Telegram channel. VK URL is required and stored first
    /// (it wins the embed); YouTube URL is optional. Saves immediately.
    /// Returns Duplicate=true when a video with the same VK id already exists.
    /// </summary>
    public async Task<TelegramImportResult> ImportFromTelegramAsync(
        string title,
        string vkUrl,
        string? youTubeUrl,
        string? description,
        long telegramMessageId,
        DateTime postDateUtc)
    {
        var vkIdMatch = Regex.Match(vkUrl, @"video-?(\d+_\d+)", RegexOptions.IgnoreCase);
        if (!vkIdMatch.Success)
        {
            logger.LogWarning("Cannot extract VK video id from URL {Url} (telegram post {MessageId})", vkUrl, telegramMessageId);
            return new TelegramImportResult(null, false, $"Не удалось извлечь id видео из VK URL: {vkUrl}");
        }
        var vkVideoId = vkIdMatch.Groups[1].Value;

        if (await context.Videos.AnyAsync(v => v.VideoUrls.Contains(vkVideoId)))
        {
            logger.LogInformation("Video {VkVideoId} from telegram post {MessageId} already exists, skipping", vkVideoId, telegramMessageId);
            return new TelegramImportResult(null, true, null);
        }

        VideoMetadata? meta = null;
        try
        {
            meta = await GetVideoMetadataAsync(vkUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Metadata fetch failed for {Url} (telegram post {MessageId}); importing with minimal data", vkUrl, telegramMessageId);
        }

        if (!string.IsNullOrWhiteSpace(youTubeUrl) && !IsYouTubeUrl(youTubeUrl))
        {
            logger.LogWarning("Ignoring non-YouTube URL {Url} from telegram post {MessageId}", youTubeUrl, telegramMessageId);
            youTubeUrl = null;
        }

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title.Trim() : meta?.Title;
        if (string.IsNullOrWhiteSpace(resolvedTitle))
        {
            logger.LogWarning("No title available for {Url} (telegram post {MessageId})", vkUrl, telegramMessageId);
            return new TelegramImportResult(null, false, $"Не удалось получить название: {vkUrl}");
        }

        var video = new Video
        {
            Title = resolvedTitle,
            SEOId = SeoIdHelper.FromTitle(resolvedTitle),
            Description = !string.IsNullOrWhiteSpace(description) ? description.Trim() : meta?.Description ?? "",
            ImageUrl = meta?.Thumbnail ?? "",
            PublishDate = meta?.PublishDate ?? DateTime.SpecifyKind(postDateUtc, DateTimeKind.Utc),
            VideoUrls = VideoUrlHelper.NormalizeVideoUrls(
                string.IsNullOrWhiteSpace(youTubeUrl) ? vkUrl : $"{vkUrl}; {youTubeUrl}"),
            Status = VideoStatus.Published,
            Keywords = ""
        };
        context.Videos.Add(video);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Imported video from telegram post {MessageId}: Title={Title}, SEOId={SeoId}, PublishDate={PublishDate:o}, HasMetadata={HasMetadata}",
            telegramMessageId, video.Title, video.SEOId, video.PublishDate, meta != null);
        return new TelegramImportResult(video, false, null);
    }

    /// <summary>Imports videos by URL (one per line). Caller is responsible for SaveChangesAsync.</summary>
    public async Task<(int imported, List<string> errors)> ImportByUrlsAsync(string urlsText)
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
            logger.LogWarning("No http/https URLs found in import text.");
            errors.Add("Не найдено ни одного URL (http/https).");
            return (0, errors);
        }

        logger.LogInformation("Importing {UrlCount} distinct URL(s).", urls.Count);

        foreach (var url in urls)
        {
            logger.LogInformation("Processing import URL: {Url}", url);
            var host = ImportHost(url);

            if (!IsYouTubeUrl(url) && !IsVkUrl(url))
            {
                logger.LogWarning("Unsupported host for import: {Url}", url);
                errors.Add($"Неподдерживаемый URL (только YouTube и VK): {url}");
                metrics.IncrementVideosImported("error", host);
                continue;
            }

            VideoMetadata? meta;
            try
            {
                meta = await GetVideoMetadataAsync(url);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Metadata fetch failed for {Url}", url);
                errors.Add($"{url}: {ex.Message}");
                metrics.IncrementVideosImported("error", host);
                continue;
            }

            if (meta == null)
            {
                logger.LogWarning("No metadata returned for {Url}", url);
                errors.Add($"{url}: не удалось получить метаданные.");
                metrics.IncrementVideosImported("error", host);
                continue;
            }
            if (string.IsNullOrWhiteSpace(meta.Title))
            {
                logger.LogWarning("Empty title in metadata for {Url}", url);
                errors.Add($"Не удалось получить название: {url}");
                metrics.IncrementVideosImported("error", host);
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
            metrics.IncrementVideosImported("success", host);
            logger.LogInformation(
                "Queued video for save: Title={Title}, SEOId={SeoId}, PublishDate={PublishDate:o}",
                video.Title,
                video.SEOId,
                video.PublishDate);
        }

        return (imported, errors);
    }

    private static string ImportHost(string url)
    {
        if (IsYouTubeUrl(url))
            return "youtube";
        if (IsVkUrl(url))
            return "vk";
        return "other";
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
        return (url.Contains("vk.com/", StringComparison.OrdinalIgnoreCase)
                || url.Contains("vk.ru/", StringComparison.OrdinalIgnoreCase)
                || url.Contains("vkvideo.ru/", StringComparison.OrdinalIgnoreCase))
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
            logger.LogDebug("Using Kafka MediaInfo for {Url}", url);
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
        logger.LogDebug("Using local yt-dlp for {Url}", url);
        return await GetVideoMetadataViaYtDlpAsync(url);
    }

    private string YtDlpPath =>
        configuration["VideoImport:YtDlpPath"] is { Length: > 0 } configured
            ? configured
            : "yt-dlp";

    private async Task<VideoMetadata> GetVideoMetadataViaYtDlpAsync(string url)
    {
        var ytDlpPath = YtDlpPath;
        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            ArgumentList = { "--no-download", "-j", "-q", url },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = StartYtDlp(startInfo, ytDlpPath);

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            logger.LogWarning(
                "yt-dlp exited with code {ExitCode} for {Url}. Stderr: {Stderr}",
                process.ExitCode,
                url,
                string.IsNullOrWhiteSpace(stderr) ? "(empty)" : stderr.Trim());
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "yt-dlp вернул ошибку." : stderr.Trim());
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            logger.LogWarning("yt-dlp returned no stdout for {Url}", url);
            throw new InvalidOperationException("yt-dlp не вернул данные.");
        }

        var json = JObject.Parse(stdout);
        var title = json["title"]?.ToString();
        var thumbnail = json["thumbnail"]?.ToString();
        var description = json["description"]?.ToString();

        if (string.IsNullOrWhiteSpace(thumbnail) && TryGetYouTubeVideoId(url, out var ytId))
            thumbnail = BuildYouTubeThumbnailUrl(ytId);

        var publishDate = ParsePublishDate(json);

        return new VideoMetadata(title, thumbnail, description, publishDate);
    }

    private Process StartYtDlp(ProcessStartInfo startInfo, string ytDlpPath)
    {
        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            logger.LogError(ex, "yt-dlp ({YtDlpPath}) could not be started.", ytDlpPath);
            throw new InvalidOperationException(YtDlpMissingMessage(ytDlpPath), ex);
        }

        if (process == null)
        {
            logger.LogError("yt-dlp ({YtDlpPath}) could not be started (not in PATH or blocked).", ytDlpPath);
            throw new InvalidOperationException(YtDlpMissingMessage(ytDlpPath));
        }

        return process;
    }

    private static string YtDlpMissingMessage(string ytDlpPath) =>
        $"Не удалось запустить yt-dlp ('{ytDlpPath}'). Установите yt-dlp и добавьте его в PATH " +
        "или укажите полный путь в настройке VideoImport:YtDlpPath.";

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
