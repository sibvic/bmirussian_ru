using Minio;
using Minio.DataModel.Args;

namespace BMIRussian_ru.Services;

public class MinioService(Microsoft.Extensions.Options.IOptions<MinioOptions> options) : IMinioService
{
    private readonly MinioOptions _options = options.Value;
    private readonly string _bucket = options.Value.Bucket;

    private IMinioClient CreateClient() => new MinioClient()
        .WithEndpoint(_options.Endpoint)
        .WithCredentials(_options.AccessKey, _options.SecretKey)
        .WithSSL(_options.UseSsl)
        .Build();

    public async Task<string> UploadTranscriptAsync(long videoId, Stream content, long contentLength, string contentType, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();

        var beArgs = new BucketExistsArgs().WithBucket(_bucket);
        var found = await client.BucketExistsAsync(beArgs, cancellationToken).ConfigureAwait(false);
        if (!found)
        {
            var mbArgs = new MakeBucketArgs().WithBucket(_bucket);
            await client.MakeBucketAsync(mbArgs, cancellationToken).ConfigureAwait(false);
        }

        var objectName = $"transcripts/{videoId}.txt";

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(contentLength)
            .WithContentType(contentType);
        await client.PutObjectAsync(putArgs, cancellationToken).ConfigureAwait(false);

        return objectName;
    }

    public async Task<string?> GetTranscriptContentAsync(long videoId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var objectName = $"transcripts/{videoId}.txt";
        string? content = null;
        try
        {
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectName)
                .WithCallbackStream(s =>
                {
                    using var reader = new StreamReader(s);
                    content = reader.ReadToEnd();
                });
            await client.GetObjectAsync(getArgs, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
        return content;
    }
}
