using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BMIRussian_ru.Data;

namespace BMIRussian_ru.Services;

/// <summary>
/// EF-backed store for STL generation jobs. The web app creates jobs (Queued);
/// the StlWorker result consumer marks them Done/Failed.
/// </summary>
public class StlJobStore(ApplicationDbContext context, IOptions<ShopOptions> options, MetricsService metrics)
{
    private TimeSpan StaleTimeout => TimeSpan.FromMinutes(Math.Max(1, options.Value.StaleTimeoutMinutes));

    public async Task<StlJob> CreateAsync(string requestId, long userId, string model, string text)
    {
        var job = new StlJob
        {
            RequestId = requestId,
            UserId = userId,
            Model = model,
            Text = text,
            Status = StlJobStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };
        context.StlJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    public Task<StlJob?> GetAsync(string requestId) =>
        context.StlJobs.FirstOrDefaultAsync(j => j.RequestId == requestId);

    /// <summary>True when the user has a job still in queue (stale jobs don't count).</summary>
    public Task<bool> HasActiveJobAsync(long userId)
    {
        var cutoff = DateTime.UtcNow - StaleTimeout;
        return context.StlJobs.AnyAsync(j =>
            j.UserId == userId && j.Status == StlJobStatus.Queued && j.CreatedAt > cutoff);
    }

    /// <summary>True when a queued job has waited longer than the configured stale timeout.</summary>
    public bool IsStale(StlJob job) =>
        job.Status == StlJobStatus.Queued && job.CreatedAt <= DateTime.UtcNow - StaleTimeout;

    public async Task<StlJob?> SetDoneAsync(string requestId, string objectName)
    {
        var job = await GetAsync(requestId);
        if (job == null) return null;
        job.Status = StlJobStatus.Done;
        job.ObjectName = objectName;
        job.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        metrics.ObserveStlJobDuration(job.Model, "done", DurationSeconds(job));
        return job;
    }

    public async Task<StlJob?> SetFailedAsync(string requestId, string error)
    {
        var job = await GetAsync(requestId);
        if (job == null) return null;
        job.Status = StlJobStatus.Failed;
        job.Error = error;
        job.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        metrics.ObserveStlJobDuration(job.Model, "failed", DurationSeconds(job));
        return job;
    }

    private static double DurationSeconds(StlJob job) =>
        ((job.CompletedAt ?? DateTime.UtcNow) - job.CreatedAt).TotalSeconds;

    public Task<List<StlJob>> ListByUserAsync(long userId, int count = 50) =>
        context.StlJobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(count)
            .ToListAsync();
}
