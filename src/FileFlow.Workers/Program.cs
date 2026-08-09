using FileFlow.Application;
using FileFlow.Data;
using FileFlow.Workers;
using FileFlow.Workers.Consumers;
using FileFlow.Workers.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddDataConfig()
    .AddAmazonS3();

builder.Services.AddApplication(builder.Configuration);

builder.Services.AddTransient<AuditConsumer>();
builder.Services.AddTransient<FileManagementConsumer>();
builder.Services.AddTransient<DomainEventConsumer>();
builder.Services.AddHostedService<TemporaryBucketCleanupWorker>();

var host = builder.Build();

host.Run();