namespace BMIRussian_ru.Services;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool UseSsl { get; set; } = false;
    public string Bucket { get; set; } = "bmi";
}
