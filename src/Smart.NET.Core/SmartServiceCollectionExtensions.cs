using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Smart.NET.Core;

/// <summary>Registers the provider-independent Smart.NET.Core services.</summary>
public static class SmartServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISmart"/>. An <see cref="Smart.NET.Abstractions.ISmartProvider"/>
    /// must also be registered by the application or a provider package.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSmart(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ISmart, SmartService>();
        return services;
    }
}
