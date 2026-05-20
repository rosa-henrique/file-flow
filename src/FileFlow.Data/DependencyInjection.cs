using FileFlow.Data.Context;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FileFlow.Data;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddDataConfig(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<FileFlowDbContext>("fileflow");
        builder.EnrichNpgsqlDbContext<FileFlowDbContext>();

        return builder;
    }
}