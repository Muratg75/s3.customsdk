namespace S3CustomSDK.Core;

public class S3Config
{
    public string Endpoint { get; set; }
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public string Region { get; set; } = "us-east-1"; // Default region
}
