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
        return AddSmartJevCore(services, configure);
    }

    /// <summary>
    /// Registers Jev through OpenRouter, using <c>OPENROUTER_API_KEY</c> by default.
    /// Call <c>AddSmart()</c> from the Smart.NET package to register the core service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional settings that override the OpenRouter defaults.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSmartJevOpenRouter(
        this IServiceCollection services,
        Action<JevOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return AddSmartJevCore(
            services,
            options =>
            {
                options.ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
                options.BaseUrl = "https://openrouter.ai/api";
                options.Model = "typesafe/jev-1.13";
                configure?.Invoke(options);
            });
    }

    private static IServiceCollection AddSmartJevCore(
        IServiceCollection services,
        Action<JevOptions>? configure)
    {
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
