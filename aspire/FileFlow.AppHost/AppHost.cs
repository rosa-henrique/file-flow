using FileFlow.AppHost.Extension;

var builder = DistributedApplication.CreateBuilder(args);

var rustFs = builder.AddRustFsContainer("rustfs")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithPgWeb()
    .WithLifetime(ContainerLifetime.Persistent);

var fileFlowDb = postgres.AddDatabase("fileflow", "file_flow");

var migrations = builder.AddProject<Projects.FileFlow_MigrationService>("migrations")
    .WithReference(fileFlowDb)
    .WaitFor(fileFlowDb);

var api = builder.AddProject<Projects.FileFlow_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(rustFs)
    .WithReference(fileFlowDb)
    .WaitFor(rustFs)
    .WaitForCompletion(migrations);

builder.AddJavaScriptApp("frontend", "../../src/frontend")
    .WithRunScript("start")
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();