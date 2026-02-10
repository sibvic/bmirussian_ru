using System.Collections.Concurrent;

namespace BMIRussian_ru.Services;

/// <summary>
/// In-memory store for MediaInfo request results. Consumer writes here; callers wait for result by RequestId.
/// </summary>
public class MediaInfoResultStore
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<MediaInfoResult?>> _pending = new();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public void RegisterPending(string requestId)
    {
        _pending.TryAdd(requestId, new TaskCompletionSource<MediaInfoResult?>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    public void SetResult(string requestId, MediaInfoResult result)
    {
        if (_pending.TryRemove(requestId, out var tcs))
            tcs.TrySetResult(result);
    }

    public void SetFailed(string requestId, string error)
    {
        if (_pending.TryRemove(requestId, out var tcs))
            tcs.TrySetResult(new MediaInfoResult { Error = error });
    }

    public async Task<MediaInfoResult?> WaitForResultAsync(string requestId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!_pending.TryGetValue(requestId, out var tcs))
            return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(requestId, out _);
            return null;
        }
    }
}

public class MediaInfoResult
{
    public string? Title { get; set; }
    public string? Thumbnail { get; set; }
    public string? Description { get; set; }
    public DateTime PublishDate { get; set; }
    public string? Error { get; set; }
    public bool IsSuccess => string.IsNullOrEmpty(Error);
}
