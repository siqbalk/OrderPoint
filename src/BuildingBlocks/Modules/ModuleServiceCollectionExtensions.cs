using System.Reflection;
using BuildingBlocks.Behaviors;
using BuildingBlocks.Validation;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Modules;

public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module's Application assembly: its MediatR handlers and its
    /// FluentValidation validators. The shared pipeline behaviors are registered
    /// with TryAddEnumerable so they run once per request no matter how many
    /// modules call this.
    /// </summary>
    public static IServiceCollection AddModuleApplication(this IServiceCollection services, Assembly applicationAssembly)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        return services;
    }
}
