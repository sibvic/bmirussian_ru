using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace BMIRussian_ru.Services;

public enum StlJobStatus
{
    Queued,
    Done,
    Failed
}

public class StlJob
{
    public string RequestId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Text { get; set; } = "";
    public StlJobStatus Status { get; set; } = StlJobStatus.Queued;
    public string? ObjectName { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// In-memory store for STL generation jobs. Producer creates jobs; result consumer updates them;
/// pages poll status and download via ObjectName.
/// </summary>
public class StlJobStore(IOptions<ShopOptions> options)
{
    private readonly ConcurrentDictionary<string, StlJob> _jobs = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(Math.Max(1, options.Value.JobRetentionHours));

    public StlJob Create(string requestId, string ownerId, string text)
    {
        Sweep();
        var job = new StlJob { RequestId = requestId, OwnerId = ownerId, Text = text };
        _jobs[requestId] = job;
        return job;
    }

    public StlJob? Get(string requestId)
    {
        return _jobs.TryGetValue(requestId, out var job) ? job : null;
    }

    public bool HasActiveJob(string ownerId)
    {
        Sweep();
        return _jobs.Values.Any(j => j.OwnerId == ownerId && j.Status == StlJobStatus.Queued);
    }

    public void SetDone(string requestId, string objectName)
    {
        if (_jobs.TryGetValue(requestId, out var job))
        {
            job.Status = StlJobStatus.Done;
            job.ObjectName = objectName;
        }
    }

    public void SetFailed(string requestId, string error)
    {
        if (_jobs.TryGetValue(requestId, out var job))
        {
            job.Status = StlJobStatus.Failed;
            job.Error = error;
        }
    }

    private void Sweep()
    {
        var cutoff = DateTime.UtcNow - _ttl;
        foreach (var pair in _jobs)
        {
            if (pair.Value.CreatedAt < cutoff)
                _jobs.TryRemove(pair.Key, out _);
        }
    }
}
