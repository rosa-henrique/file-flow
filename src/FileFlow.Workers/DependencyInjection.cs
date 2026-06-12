using Amazon.S3;

namespace FileFlow.Workers;

public static class DependencyInjection
{
    internal static IHostApplicationBuilder AddAmazonS3(this IHostApplicationBuilder builder)
    {
        var s3Uri = builder.Configuration.GetValue<string>("RUSTFS_URI");
        var accessKey = builder.Configuration.GetValue<string>("RUSTFS_ACCESSKEY");
        var secretKey = builder.Configuration.GetValue<string>("RUSTFS_SECRETKEY");

        builder.Services.AddSingleton<IAmazonS3>(_ =>
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = s3Uri,
                ForcePathStyle = true,
            };
            return new AmazonS3Client(accessKey, secretKey, s3Config);
        });

        return builder;
    }
}