using FileFlow.Application.Behaviors;
using FileFlow.Data.Context;

using FluentValidation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssembly(assembly);

            // options.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>));
            options.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddCap(options =>
        {
            options.UseEntityFramework<FileFlowDbContext>();
            options.UseRabbitMQ(opt =>
            {
                opt.ConnectionFactoryOptions = x =>
                {
                    x.HostName = configuration["RABBITMQ_HOST"]!;
                    x.Port = int.Parse(configuration["RABBITMQ_PORT"]!);
                    x.UserName = configuration["RABBITMQ_USERNAME"]!;
                    x.Password = configuration["RABBITMQ_PASSWORD"]!;
                };
            });
            options.UseDashboard(opt =>
            {
                opt.AllowAnonymousExplicit = true;
            });
        });

        return services;
    }
}