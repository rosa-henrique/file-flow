using FileFlow.Data.Context;
using FileFlow.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<Worker>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<FileFlowDbContext>("fileflow", configureDbContextOptions: options =>
{
    options.UseNpgsql(o => o.MigrationsAssembly("FileFlow.MigrationService"));
});

var host = builder.Build();
host.Run();