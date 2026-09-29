using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Smart.NET.Core;
using Smart.NET.Abstractions;
using Smart.NET.Jev;

namespace Smart.NET.Jev.Tests;

public sealed class JevSmartProviderTests
{
    [Fact]
    public async Task NoulMapsProbabilityAndUsesOfficialRequestShape()
    {
        HttpRequestMessage? sentRequest = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            sentRequest = request;
            var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var body = JsonDocument.Parse(requestBody);

            Assert.Equal("/v1/systemone", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer test-api-key", request.Headers.Authorization?.ToString());
            Assert.Equal("jev-1.13.0", body.RootElement.GetProperty("model").GetString());
            Assert.Equal(
                "noul",
                body.RootElement.GetProperty("questions").GetProperty("decision").GetProperty("type").GetString());
            Assert.Equal("AAPL", body.RootElement.GetProperty("state").GetProperty("Symbol").GetString());

            return JsonResponse("""
                {
                  "model": "jev-1.13.0",
                  "answers": { "decision": { "type": "noul", "noul": 0.82 } },
                  "usage": { "input_tokens": 10, "output_tokens": 4 }
                }
                """);
        });
        var smart = CreateSmart(handler);

        var result = await smart.IfResult(
            new { Symbol = "AAPL" },
            "Should this order be manually reviewed?");

        Assert.True(result.Value);
        Assert.False(result.UsedFallback);
        Assert.Equal("jev", result.Provider);
        Assert.Equal("jev-1.13.0", result.Model);
        Assert.Equal(0.82, result.Probability);
        Assert.Equal(10, result.Metadata["input_tokens"]);
        Assert.NotNull(sentRequest);
    }

    [Fact]
    public async Task OpenRouterUsesItsSystemOneEndpointAndAuthorizationKey()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal("https://openrouter.ai/api/v1/systemone", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer openrouter-test-key", request.Headers.Authorization?.ToString());
            return Task.FromResult(JsonResponse("""
                {
                  "id": "gen-dec-test",
                  "model": "typesafe/jev-1.13-20260917",
                  "provider": "TypeSafe",
                  "answers": { "decision": { "type": "noul", "noul": 0.9 } },
                  "usage": { "input_tokens": 1, "output_tokens": 1, "cost": 0.000001 }
                }
                """));
        });
        var client = new JevHttpClient(
            new HttpClient(handler),
            Options.Create(new JevOptions
            {
                ApiKey = "openrouter-test-key",
                BaseUrl = "https://openrouter.ai/api",
                Model = "typesafe/jev-1.13"
            }));
        var smart = new SmartService(new JevSmartProvider(client));

        Assert.True(await smart.If("state", "Should this proceed?"));
    }

    [Fact]
    public async Task ChoiceMapsBackToStronglyTypedEnumAndKeepsConfidenceSeparate()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            var bodyText = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var body = JsonDocument.Parse(bodyText);
            var criteria = body.RootElement.GetProperty("questions")
                .GetProperty("decision")
                .GetProperty("criteria");
            var selected = criteria.EnumerateObject().Single(option => option.Value.GetString() == "Hold").Name;
            var response = JsonSerializer.Serialize(new
            {
                model = "jev-1.13.0",
                answers = new
                {
                    decision = new
                    {
                        type = "choice",
                        choice = selected,
                        confidence = 0.91,
                        probabilities = new Dictionary<string, double> { [selected] = 0.94 }
                    }
                },
                usage = new { input_tokens = 1, output_tokens = 1 }
            });

            return JsonResponse(response);
        });
        var smart = CreateSmart(handler);

        var result = await smart.SwitchResult(
            "Market state",
            "What action should be considered?",
            [TradeAction.Buy, TradeAction.Hold, TradeAction.Sell]);

        Assert.Equal(TradeAction.Hold, result.Value);
        Assert.Equal(0.94, result.Probability);
        Assert.Equal(0.91, result.Confidence);
    }

    [Fact]
    public async Task ScoreMapsOfficialOrdinalRubricToRequestedRange()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            var bodyText = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var body = JsonDocument.Parse(bodyText);
            var criteria = body.RootElement.GetProperty("questions")
                .GetProperty("decision")
                .GetProperty("criteria");
            Assert.Equal(10, criteria.GetArrayLength());
            Assert.Equal("10", criteria[0].GetString());
            Assert.Equal("100", criteria[9].GetString());

            return JsonResponse("""
                {
                  "model": "jev-1.13.0",
                  "answers": {
                    "decision": {
                      "type": "score",
                      "score": 4.5,
                      "confidence": 0.77,
                      "legend": {},
                      "probabilities": {}
                    }
                  },
                  "usage": { "input_tokens": 1, "output_tokens": 1 }
                }
                """);
        });
        var smart = CreateSmart(handler);

        var result = await smart.ScoreResult("Signal", "How strong is this?", 10, 100);

        Assert.Equal(55, result.Value);
        Assert.Equal(0.77, result.Confidence);
    }

    [Fact]
    public async Task RateLimitRetriesOnlyWhenTheCallerEnablesRetries()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                var limited = new HttpResponseMessage((HttpStatusCode)429);
                limited.Headers.TryAddWithoutValidation("retry-after-ms", "0");
                return Task.FromResult(limited);
            }

            return Task.FromResult(JsonResponse("""
                {
                  "model": "jev-1.13.0",
                  "answers": { "decision": { "type": "noul", "noul": 0.2 } },
                  "usage": { "input_tokens": 1, "output_tokens": 1 }
                }
                """));
        });
        var smart = CreateSmart(handler);

        var value = await smart.If(
            "state",
            "Is this likely?",
            options => options.WithRetries(1));

        Assert.False(value);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AuthenticationFailureMapsToProviderIndependentConfigurationError()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var smart = CreateSmart(handler);

        var exception = await Assert.ThrowsAsync<SmartConfigurationException>(
            () => smart.If("state", "Is this likely?"));

        Assert.IsType<JevException>(exception.InnerException);
    }

    [Fact]
    public void ProviderRejectsMissingApiKeyWithoutMakingARequest()
    {
        var handler = new StubHandler((_, _) =>
            throw new InvalidOperationException("An HTTP request should not be sent."));

        Assert.Throws<SmartConfigurationException>(() => new JevHttpClient(
            new HttpClient(handler),
            Options.Create(new JevOptions { ApiKey = " " })));
    }

    [Fact]
    public void AddSmartJevRegistersTheAdapterWithoutRegisteringCoreServices()
    {
        var services = new ServiceCollection();
        services.AddSmartJev(options => options.ApiKey = "test-api-key");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<JevSmartProvider>(provider.GetRequiredService<ISmartProvider>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ISmart>());
    }

    [Fact]
    public void AddSmartJevOpenRouterUsesOpenRouterEnvironmentKeyAndSystemOneDefaults()
    {
        var previousKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "openrouter-test-key");

        try
        {
            var services = new ServiceCollection();
            services.AddSmartJevOpenRouter();
            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<JevOptions>>().Value;

            Assert.Equal("openrouter-test-key", options.ApiKey);
            Assert.Equal("https://openrouter.ai/api", options.BaseUrl);
            Assert.Equal("typesafe/jev-1.13", options.Model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", previousKey);
        }
    }

    private static SmartService CreateSmart(HttpMessageHandler handler)
    {
        var client = new JevHttpClient(
            new HttpClient(handler),
            Options.Create(new JevOptions
            {
                ApiKey = "test-api-key",
                BaseUrl = "https://api.typesafe.ai",
                Model = "jev-1.13.0"
            }));
        return new SmartService(new JevSmartProvider(client));
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }

    private enum TradeAction
    {
        Buy,
        Hold,
        Sell
    }
}
