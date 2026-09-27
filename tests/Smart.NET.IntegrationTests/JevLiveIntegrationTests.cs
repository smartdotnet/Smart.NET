using Microsoft.Extensions.Options;
using Smart.NET;
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
}
