namespace BMIRussian_ru.Services;

public interface IMinioService
{
    /// <summary>Uploads a transcript file to the BMI bucket and returns the object key.</summary>
    Task<string> UploadTranscriptAsync(long videoId, Stream content, long contentLength, string contentType, CancellationToken cancellationToken = default);
    Task<string?> GetTranscriptContentAsync(long videoId, CancellationToken cancellationToken = default);
}
