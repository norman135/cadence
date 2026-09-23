using Cadence.Application.Common.Behaviors;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cadence.Application;

public static class DependencyInjection
{
    /// <summary>Registers the mediator, the request pipeline and all validators of the application layer.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "Cadence.Application.Mediator";
            // Handlers depend on scoped services such as the DbContext.
            options.ServiceLifetime = ServiceLifetime.Scoped;
            // Order matters: logging wraps validation, so rejected requests are logged too.
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
            ];
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
