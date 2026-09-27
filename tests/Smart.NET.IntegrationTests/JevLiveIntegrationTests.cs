using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Smart.NET;
using Smart.NET.Abstractions;
using Smart.NET.Jev;

namespace Smart.NET.IntegrationTests;

public sealed class JevLiveIntegrationTests
{
    [Fact]
    public async Task JevCanAnswerNoulQuestionWhenExplicitlyEnabled()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("SMART_JEV_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("SMART_JEV_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(apiKey), "Set SMART_JEV_API_KEY to run this live test.");

        using var httpClient = new HttpClient();
        var provider = new JevSmartProvider(new JevHttpClient(
            httpClient,
            Options.Create(new JevOptions { ApiKey = apiKey })));
        var smart = new SmartService(provider);

        var result = await smart.IfResult(
            new { Message = "The server has been unavailable for an hour." },
            "Does this message describe an operational incident?",
            options => options.WithTimeout(TimeSpan.FromSeconds(15)),
            cancellationToken: CancellationToken.None);

        Assert.InRange(result.Probability!.Value, 0, 1);
    }

    [Fact]
    public async Task JevCanAnswerThroughOpenRouterWhenExplicitlyEnabled()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("SMART_OPENROUTER_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            return;           
        }

        var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(apiKey), "Set OPENROUTER_API_KEY to run this live test.");

        var services = new ServiceCollection();
        services.AddSmart();
        services.AddSmartJevOpenRouter(options =>
        {
            options.ApiKey = apiKey;
            options.Model = Environment.GetEnvironmentVariable("OPENROUTER_JEV_MODEL")
                ?? "typesafe/jev-1.13";
        });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var smart = scope.ServiceProvider.GetRequiredService<ISmart>();

        var result = await smart.IfResult(
            new { Message = "The service is unavailable and customers cannot sign in." },
            "Does this message describe an operational incident?",
            options => options.WithTimeout(TimeSpan.FromSeconds(20)),
            cancellationToken: CancellationToken.None);

        Assert.False(result.UsedFallback);
        Assert.Equal("jev", result.Provider);
        Assert.StartsWith("typesafe/jev-", result.Model, StringComparison.Ordinal);
        Assert.InRange(result.Probability!.Value, 0, 1);
    }
}
