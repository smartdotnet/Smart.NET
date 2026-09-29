using Microsoft.Extensions.DependencyInjection;
using Smart.NET.Abstractions;
using Smart.NET.Core;

namespace Smart.NET.Tests;

public sealed class SmartServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IfReturnsExplicitBoolean(bool expected)
    {
        var service = CreateService(new FakeSmartProvider().When(
            "decision",
            new SmartDecision { Kind = SmartDecisionKind.Boolean, BooleanValue = expected }));

        Assert.Equal(expected, await service.If(new { OrderId = 42 }, "decision"));
    }

    [Theory]
    [InlineData(0.85, 0.85, true)]
    [InlineData(0.84, 0.85, false)]
    public async Task IfUsesProbabilityOnlyWhenThresholdIsConfigured(
        double probability,
        double threshold,
        bool expected)
    {
        var provider = new FakeSmartProvider().When(
            "threshold",
            new SmartDecision { Kind = SmartDecisionKind.Boolean, Probability = probability });
        var service = CreateService(provider);

        var result = await service.IfResult(
            "context",
            "threshold",
            options => options.WithThreshold(threshold));

        Assert.Equal(expected, result.Value);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task IfUsesDocumentedHalfProbabilityDefault()
    {
        var service = CreateService(new FakeSmartProvider().When(
            "invalid boolean",
            new SmartDecision { Kind = SmartDecisionKind.Boolean, Probability = 0.9 }));

        Assert.True(await service.If("context", "invalid boolean"));
    }

    [Fact]
    public async Task IfKeepsExplicitBooleanWhenProviderHasNoProbability()
    {
        var service = CreateService(new FakeSmartProvider().When(
            "explicit boolean",
            new SmartDecision { Kind = SmartDecisionKind.Boolean, BooleanValue = false }));

        Assert.False(await service.If(
            "context",
            "explicit boolean",
            options => options.WithThreshold(0.9)));
    }

    [Fact]
    public async Task IfSerializesContextOnceAndPassesOperationConstraints()
    {
        var provider = new FakeSmartProvider().When(
            "serialized",
            new SmartDecision
            {
                Kind = SmartDecisionKind.Boolean,
                BooleanValue = true,
                Probability = 0.9
            });
        var service = CreateService(provider);

        await service.If(
            new Context { Id = 7 },
            "serialized",
            options => options.WithThreshold(0.7));

        Assert.Equal(SmartOperation.Boolean, provider.Requests.Single().Operation);
        Assert.Equal(7, provider.Requests.Single().Context?.GetProperty("Id").GetInt32());
        Assert.Equal(0.7, provider.Requests.Single().Options.Threshold);
    }

    [Fact]
    public async Task IfAcceptsNullContext()
    {
        var provider = new FakeSmartProvider().When(
            "null context",
            new SmartDecision { Kind = SmartDecisionKind.Boolean, BooleanValue = true });
        var service = CreateService(provider);

        Assert.True(await service.If<string?>(null, "null context"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, provider.Requests.Single().Context?.ValueKind);
    }

    [Fact]
    public async Task SwitchReturnsOriginalTypedEnumChoice()
    {
        var choices = new[] { TradeAction.Buy, TradeAction.Hold, TradeAction.Sell };
        var chosenValue = System.Text.Json.JsonSerializer.Serialize(TradeAction.Hold);
        var service = CreateService(new FakeSmartProvider().When(
            "choose",
            new SmartDecision { Kind = SmartDecisionKind.Choice, ChoiceValue = chosenValue }));

        var choice = await service.Switch(new { Symbol = "ABC" }, "choose", choices);

        Assert.Equal(TradeAction.Hold, choice);
    }

    [Fact]
    public async Task SwitchRejectsChoiceOutsideAllowedSet()
    {
        var service = CreateService(new FakeSmartProvider().When(
            "invalid choice",
            new SmartDecision { Kind = SmartDecisionKind.Choice, ChoiceValue = "\"outside\"" }));

        await Assert.ThrowsAsync<SmartInvalidDecisionException>(
            () => service.Switch("context", "invalid choice", "Buy", "Sell"));
    }

    [Fact]
    public async Task SwitchRejectsDuplicateAndEmptyChoiceListsBeforeCallingProvider()
    {
        var provider = new FakeSmartProvider();
        var service = CreateService(provider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Switch("context", "duplicate", "Buy", "Buy"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Switch("context", "empty", Array.Empty<string>()));
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task SwitchChoiceMatchingIsCaseSensitive()
    {
        var service = CreateService(new FakeSmartProvider().When(
            "case",
            new SmartDecision { Kind = SmartDecisionKind.Choice, ChoiceValue = "\"buy\"" }));

        await Assert.ThrowsAsync<SmartInvalidDecisionException>(
            () => service.Switch("context", "case", "Buy", "Sell"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task ScoreAcceptsInclusiveBounds(double score)
    {
        var service = CreateService(new FakeSmartProvider().When(
            "score",
            new SmartDecision { Kind = SmartDecisionKind.Score, Score = score }));

        Assert.Equal(score, await service.Score("context", "score", 0, 100));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public async Task ScoreRejectsOutOfRangeOrNonFiniteValues(double score)
    {
        var service = CreateService(new FakeSmartProvider().When(
            "bad score",
            new SmartDecision { Kind = SmartDecisionKind.Score, Score = score }));

        await Assert.ThrowsAsync<SmartInvalidDecisionException>(
            () => service.Score("context", "bad score", 0, 100));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateReturnsValidationBoolean(bool expected)
    {
        var provider = new FakeSmartProvider().When(
            "validate",
            new SmartDecision { Kind = SmartDecisionKind.Validation, BooleanValue = expected });
        var service = CreateService(provider);

        Assert.Equal(expected, await service.Validate("context", "validate"));
        Assert.Equal(SmartOperation.Validation, provider.Requests.Single().Operation);
    }

    [Fact]
    public async Task ProviderFailureReturnsObservableFallback()
    {
        var provider = new FakeSmartProvider().When(
            "fallback",
            (_, _) => throw new SmartProviderException("provider unavailable"));
        var service = CreateService(provider);

        var result = await service.IfResult(
            "context",
            "fallback",
            options => options.WithFallback(false));

        Assert.False(result.Value);
        Assert.True(result.UsedFallback);
        Assert.Null(result.Provider);
    }

    [Fact]
    public async Task ProviderFailureWithoutFallbackMapsToSmartProviderException()
    {
        var service = CreateService(new FakeSmartProvider().When(
            "provider error",
            (_, _) => throw new InvalidOperationException("private provider detail")));

        var exception = await Assert.ThrowsAsync<SmartProviderException>(
            () => service.If("context", "provider error"));

        Assert.Equal("The configured provider failed.", exception.Message);
    }

    [Fact]
    public async Task InvalidProviderDecisionReturnsFallback()
    {
        var service = CreateService(new FakeSmartProvider().When(
            "bad response",
            new SmartDecision { Kind = SmartDecisionKind.Score, Score = 3 }));

        var result = await service.ScoreResult(
            "context",
            "bad response",
            0,
            1,
            options => options.WithFallback(0.5));

        Assert.Equal(0.5, result.Value);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public async Task InvalidFallbackTypeIsReported()
    {
        var provider = new FakeSmartProvider().When(
            "bad fallback",
            (_, _) => throw new SmartProviderException("provider unavailable"));
        var service = CreateService(provider);

        await Assert.ThrowsAsync<SmartConfigurationException>(() => service.If(
            "context",
            "bad fallback",
            options => options.WithFallback("not a boolean")));
    }

    [Fact]
    public async Task TimeoutCanUseFallback()
    {
        var provider = new FakeSmartProvider().When(
            "timeout",
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new SmartDecision { Kind = SmartDecisionKind.Boolean, BooleanValue = true };
            });
        var service = CreateService(provider);

        var result = await service.IfResult(
            "context",
            "timeout",
            options => options.WithTimeout(TimeSpan.FromMilliseconds(20)).WithFallback(false));

        Assert.False(result.Value);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public async Task CallerCancellationPropagatesInsteadOfUsingFallback()
    {
        var provider = new FakeSmartProvider().When(
            "cancel",
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new SmartDecision { Kind = SmartDecisionKind.Boolean, BooleanValue = true };
            });
        var service = CreateService(provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.If(
            "context",
            "cancel",
            options => options.WithFallback(false),
            cancellation.Token));
    }

    [Fact]
    public async Task RetriesOnlyExplicitlyEnabledTransientProviderFailures()
    {
        var calls = 0;
        var provider = new FakeSmartProvider().When(
            "retry",
            (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new SmartProviderException("temporary", isTransient: true);
                }

                return Task.FromResult(new SmartDecision
                {
                    Kind = SmartDecisionKind.Boolean,
                    BooleanValue = true
                });
            });
        var service = CreateService(provider);

        Assert.True(await service.If("context", "retry", options => options.WithRetries(1)));
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task EmptyQuestionIsRejectedBeforeCallingProvider()
    {
        var provider = new FakeSmartProvider();
        var service = CreateService(provider);

        await Assert.ThrowsAsync<ArgumentException>(() => service.If("context", " "));
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task AddSmartResolvesProviderThroughDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISmartProvider>(new FakeSmartProvider().When(
            "di",
            new SmartDecision { Kind = SmartDecisionKind.Boolean, BooleanValue = true }));
        services.AddSmart();

        using var provider = services.BuildServiceProvider();
        var smart = provider.GetRequiredService<ISmart>();

        Assert.True(await smart.If("context", "di"));
    }

    private static SmartService CreateService(FakeSmartProvider provider) => new(provider);

    private sealed class Context
    {
        public int Id { get; init; }
    }

    private enum TradeAction
    {
        Buy,
        Hold,
        Sell
    }
}
