using Microsoft.Extensions.DependencyInjection;
using Smart.NET.Abstractions;

namespace Smart.NET.Jev;

/// <summary>Registers the Jev decision provider.</summary>
public static class SmartServiceCollectionExtensions
{
    /// <summary>
    /// Registers Jev as the smart provider. Call <c>AddSmart()</c> from the
    /// Smart.NET package to register the provider-independent decision service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional Jev provider configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSmartJev(
        this IServiceCollection services,
        Action<JevOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<JevOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddHttpClient<JevHttpClient>();
        services.AddScoped<ISmartProvider, JevSmartProvider>();
        return services;
    }
}
