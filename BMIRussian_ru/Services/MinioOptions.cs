namespace BMIRussian_ru.Services;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool UseSsl { get; set; } = false;
    public string BucketName { get; set; } = "bmi";
    /// <summary>Legacy alias for BucketName.</summary>
    public string Bucket { get => BucketName; set => BucketName = value; }
}
