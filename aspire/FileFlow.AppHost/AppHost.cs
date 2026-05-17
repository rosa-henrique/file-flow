var builder = DistributedApplication.CreateBuilder(args);

var minio = builder.AddMinioContainer("minio")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddJavaScriptApp("frontend", "../../src/frontend")
    .WithRunScript("start")
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();