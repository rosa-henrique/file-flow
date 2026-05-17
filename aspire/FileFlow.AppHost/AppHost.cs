var builder = DistributedApplication.CreateBuilder(args);

var minio = builder.AddMinioContainer("minio")
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.FileFlow_Api>("api")
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("frontend", "../../src/frontend")
    .WithRunScript("start")
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();