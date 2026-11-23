using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using BMIRussian_ru.Data;
using BMIRussian_ru.Services;

namespace BMIRussian_ru.Pages.Shop
{
    public class KeyTagModel(
        StlJobStore store,
        StlKafkaService stlKafka,
        TextToPngRenderer renderer,
        IMinioService minio,
        IOptions<ShopOptions> options,
        ILogger<KeyTagModel> logger,
        MetricsService metrics) : PageModel
    {
        public const string ModelName = "key_tag";

        [BindProperty]
        [Display(Name = "Текст на метке")]
        public string Text { get; set; } = "";

        public string? JobId { get; set; }
        public List<StlJob> Jobs { get; set; } = [];
        public int MaxTextLength => options.Value.MaxTextLength;
        public bool IsConfigured => stlKafka.IsConfigured;
        public bool IsStale(StlJob job) => store.IsStale(job);

        private long? CurrentUserId =>
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id"), out var id)
                ? id
                : null;

        public async Task<IActionResult> OnGetAsync(string? job, string? text)
        {
            JobId = job;
            if (!string.IsNullOrEmpty(text))
                Text = text;
            if (CurrentUserId is { } userId)
                Jobs = await store.ListByUserAsync(userId);
            return Page();
        }

        public IActionResult OnGetPreview(string? text)
        {
            var normalized = Normalize(text);
            if (normalized == null)
                return NotFound();

            var png = renderer.Render(normalized, out _, out _);
            metrics.IncrementStlPreviews();
            return File(png, "image/png");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var normalized = Normalize(Text);
            if (normalized == null)
            {
                ModelState.AddModelError(nameof(Text), $"Введите текст длиной до {MaxTextLength} символов (одна строка).");
                return Page();
            }

            if (!IsConfigured)
            {
                ModelState.AddModelError(string.Empty, "Генерация временно недоступна. Попробуйте позже.");
                return Page();
            }

            if (CurrentUserId is not { } userId)
                return Forbid();
            if (await store.HasActiveJobAsync(userId))
            {
                ModelState.AddModelError(string.Empty, "У вас уже есть генерируемая модель. Дождитесь её завершения.");
                Jobs = await store.ListByUserAsync(userId);
                return Page();
            }

            var requestId = Guid.NewGuid().ToString("N");
            await store.CreateAsync(requestId, userId, ModelName, normalized);
            if (!await stlKafka.RequestGenerateAsync(requestId, ModelName, normalized))
            {
                await store.SetFailedAsync(requestId, "Failed to enqueue generation request.");
                metrics.IncrementStlJobs(ModelName, "enqueue_failed");
                ModelState.AddModelError(string.Empty, "Не удалось отправить задание на генерацию. Попробуйте позже.");
                Jobs = await store.ListByUserAsync(userId);
                return Page();
            }

            metrics.IncrementStlJobs(ModelName, "enqueued");
            logger.LogInformation("STL generation {RequestId} enqueued by user {UserId}: {Text}", requestId, userId, normalized);
            return RedirectToPage(new { job = requestId });
        }

        public async Task<IActionResult> OnGetStatusAsync(string? jobId)
        {
            var job = string.IsNullOrEmpty(jobId) ? null : await store.GetAsync(jobId);
            if (job == null || job.UserId != CurrentUserId)
                return new JsonResult(new { status = "unknown" });

            if (store.IsStale(job))
                return new JsonResult(new { status = "failed", error = "задание не было выполнено вовремя" });

            return new JsonResult(new
            {
                status = job.Status switch
                {
                    StlJobStatus.Done => "done",
                    StlJobStatus.Failed => "failed",
                    _ => "queued"
                },
                error = job.Error
            });
        }

        public async Task<IActionResult> OnGetDownloadAsync(string? jobId)
        {
            var job = string.IsNullOrEmpty(jobId) ? null : await store.GetAsync(jobId);
            if (job == null || job.UserId != CurrentUserId || job.Status != StlJobStatus.Done || job.ObjectName == null)
                return NotFound();

            var data = await minio.GetShopObjectAsync(job.ObjectName);
            if (data == null)
            {
                logger.LogWarning("STL object {ObjectName} for job {RequestId} not found in storage", job.ObjectName, job.RequestId);
                ModelState.AddModelError(string.Empty, "Файл не найден в хранилище. Сгенерируйте заново.");
                JobId = job.RequestId;
                Jobs = await store.ListByUserAsync(job.UserId);
                return Page();
            }

            var slug = SeoIdHelper.FromTitle(job.Text).ToLowerInvariant();
            var fileName = string.IsNullOrEmpty(slug) ? "key_tag.stl" : $"key_tag_{slug}.stl";
            metrics.IncrementStlDownloads(string.IsNullOrWhiteSpace(job.Model) ? ModelName : job.Model);
            return File(data, "model/stl", fileName);
        }

        private string? Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var trimmed = text.Trim();
            if (trimmed.Length > MaxTextLength || trimmed.Contains('\n') || trimmed.Contains('\r'))
                return null;
            return trimmed;
        }
    }
}
