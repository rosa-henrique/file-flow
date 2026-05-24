using Amazon.S3;

namespace FileFlow.Api;

public static class DependencyInjection
{
    internal static IHostApplicationBuilder AddAmazonS3(this IHostApplicationBuilder builder)
    {
        var minioUri = builder.Configuration.GetValue<string>("MINIO_URI");
        var minioAccessKey = builder.Configuration.GetValue<string>("MINIO_ACCESSKEY");
        var minioSecretKey = builder.Configuration.GetValue<string>("MINIO_SECRETKEY");

        builder.Services.AddSingleton<IAmazonS3>(_ =>
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = minioUri,
                ForcePathStyle = true,
            };
            return new AmazonS3Client(minioAccessKey, minioSecretKey, s3Config);
        });

        return builder;
    }
}