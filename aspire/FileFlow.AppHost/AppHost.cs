var builder = DistributedApplication.CreateBuilder(args);

var minio = builder.AddMinioContainer("minio")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);
var fileFlowDb = postgres.AddDatabase("fileflow", "file_flow");

var migrations = builder.AddProject<Projects.FileFlow_MigrationService>("migrations")
    .WithReference(fileFlowDb)
    .WaitFor(fileFlowDb);

var api = builder.AddProject<Projects.FileFlow_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(fileFlowDb)
    .WaitForCompletion(migrations);

builder.AddJavaScriptApp("frontend", "../../src/frontend")
    .WithRunScript("start")
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();