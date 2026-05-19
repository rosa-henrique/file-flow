var builder = DistributedApplication.CreateBuilder(args);

var minio = builder.AddMinioContainer("minio")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);
var fileFlowDb = postgres.AddDatabase("fileflow", "file_flow");
var fileFlowLogs = postgres.AddDatabase("fileflowlog", "file_flow_logs");

var api = builder.AddProject<Projects.FileFlow_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(fileFlowDb)
    .WithReference(fileFlowLogs)
    .WaitFor(fileFlowDb)
    .WaitFor(fileFlowLogs);

builder.AddJavaScriptApp("frontend", "../../src/frontend")
    .WithRunScript("start")
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();