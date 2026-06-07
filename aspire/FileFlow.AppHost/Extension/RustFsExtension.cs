using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.AppHost.Extension;

public static class RustFsExtension
{
    private const string AccessKeyEnvVarName = "RUSTFS_ACCESS_KEY";
    private const string SecretKeyEnvVarName = "RUSTFS_SECRET_KEY";

    public static IResourceBuilder<RustFsContainerResource> AddRustFsContainer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? accessKey = null,
        IResourceBuilder<ParameterResource>? secretKey = null,
        int? port = null)
    {
        var accessKeyParameter = accessKey?.Resource ?? new ParameterResource("accessKey", _ => RustFsContainerResource.DefaultAccessKey);

        var secretKeyParameter = secretKey?.Resource ??
#if DEBUG
                                 new ParameterResource("secretKey", _ => "admin123");
#else
                                 ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-secretKey");
#endif

        var resource = new RustFsContainerResource(name, accessKeyParameter, secretKeyParameter);

        const int consoleTargetPort = 9001;
        string consoleAddress = $"0.0.0.0:{consoleTargetPort}";
        var builderWithResource = builder
            .AddResource(resource)
            .WithImage("rustfs/rustfs", "latest")
            .WithHttpEndpoint(targetPort: 9000, name: RustFsContainerResource.PrimaryEndpointName)
            .WithHttpEndpoint(targetPort: consoleTargetPort, name: RustFsContainerResource.ConsoleEndpointName)
            .WithEnvironment("RUSTFS_VOLUMES", "/data/rustfs0")
            .WithEnvironment("RUSTFS_ADDRESS", "0.0.0.0:9000")
            .WithEnvironment("RUSTFS_CONSOLE_ADDRESS", consoleAddress)
            .WithEnvironment("RUSTFS_CONSOLE_ENABLE", "true")
            .WithEnvironment("RUSTFS_CORS_ALLOWED_ORIGINS", "*")
            .WithEnvironment("RUSTFS_CONSOLE_CORS_ALLOWED_ORIGINS", "*")
            .WithEnvironment(AccessKeyEnvVarName, $"{resource.AccessKeyParameter}")
            .WithEnvironment(SecretKeyEnvVarName, $"{resource.SecretKeyParameter}")
            .WithEnvironment("RUSTFS_OBS_LOGGER_LEVEL", "info")
            .WithBindMount("./deploy/data/pro", "/data")
            .WithBindMount("./deploy/logs", "/app/logs");

        var endpoint = builderWithResource.Resource.GetEndpoint(RustFsContainerResource.PrimaryEndpointName);

        var healthCheckKey = $"{name}_check";

        builder.Services.AddHealthChecks()
            .AddUrlGroup(options =>
            {
                var uri = new Uri(endpoint.Url);
                options.AddUri(new Uri(uri, "/health"), setup => setup.ExpectHttpCode(200));
                options.AddUri(new Uri(uri, "/health/ready"), setup => setup.ExpectHttpCode(200));
            }, healthCheckKey);

        builderWithResource.WithHealthCheck(healthCheckKey);

        return builderWithResource;
    }
}

public sealed class RustFsContainerResource(
    string name,
    ParameterResource accessKeyParameter,
    ParameterResource secretKeyParameter)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string ConsoleEndpointName = "console";
    internal const string DefaultAccessKey = "admin";

    public ParameterResource AccessKeyParameter { get; set; } = accessKeyParameter;
    public ParameterResource SecretKeyParameter { get; private set; } = secretKeyParameter;
    private EndpointReference? _primaryEndpoint;
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);
    public ReferenceExpression ConnectionStringExpression => GetConnectionString();
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"http://{Host}:{Port}");

    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new KeyValuePair<string, ReferenceExpression>("Host", ReferenceExpression.Create($"{Host}"));
        yield return new KeyValuePair<string, ReferenceExpression>("Port", ReferenceExpression.Create($"{Port}"));
        yield return new KeyValuePair<string, ReferenceExpression>("AccessKey", ReferenceExpression.Create($"{AccessKeyParameter}"));
        yield return new KeyValuePair<string, ReferenceExpression>("SecretKey", ReferenceExpression.Create($"{SecretKeyParameter}"));
        yield return new KeyValuePair<string, ReferenceExpression>("Uri", UriExpression);
    }

    private ReferenceExpression GetConnectionString()
    {
        var builder = new ReferenceExpressionBuilder();

        builder.Append(
            $"Endpoint=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

        builder.Append($";AccessKey={AccessKeyParameter}");
        builder.Append($";SecretKey={SecretKeyParameter}");

        return builder.Build();
    }
}