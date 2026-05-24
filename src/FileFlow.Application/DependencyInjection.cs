using Microsoft.Extensions.DependencyInjection;

namespace FileFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // options.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>));
            // options.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        return services;
    }
}