using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Smart.NET.Core;
using Smart.NET.Abstractions;

var services = new ServiceCollection();
services.AddSingleton<ISmartProvider, DemoProvider>();
services.AddSmart();

using var provider = services.BuildServiceProvider();
var smart = provider.GetRequiredService<ISmart>();

var trade = new { Symbol = "AAPL", Quantity = 100, Price = 250m };
var needsReview = await smart.If(trade, "Should this trade be manually reviewed?");
var action = await smart.Switch(
    trade,
    "Which action should be considered?",
    TradeAction.Buy,
    TradeAction.Hold,
    TradeAction.Sell);
var strength = await smart.Score(trade, "How strong is this signal?", 0, 100);
var isConsistent = await smart.Validate(trade, "Is this trade internally consistent?");
var resilientDecision = await smart.If(
    trade,
    "Should a temporary provider failure be handled conservatively?",
    options => options.WithTimeout(TimeSpan.FromSeconds(1)).WithFallback(false));

Console.WriteLine($"Needs review: {needsReview}");
Console.WriteLine($"Suggested action: {action}");
Console.WriteLine($"Signal strength: {strength}");
Console.WriteLine($"Trade is consistent: {isConsistent}");
Console.WriteLine($"Fallback-enabled decision: {resilientDecision}");
Console.WriteLine("This deterministic sample suggests decisions only; it does not execute trades.");

internal enum TradeAction
{
    Buy,
    Hold,
    Sell
}

internal sealed class DemoProvider : ISmartProvider
{
    public Task<SmartDecision> DecideAsync(
        SmartRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var decision = request.Operation switch
        {
            SmartOperation.Boolean => new SmartDecision
            {
                Kind = SmartDecisionKind.Boolean,
                BooleanValue = true,
                Provider = "sample"
            },
            SmartOperation.Choice => new SmartDecision
            {
                Kind = SmartDecisionKind.Choice,
                ChoiceValue = request.Choices?.LastOrDefault()?.Value,
                Provider = "sample"
            },
            SmartOperation.Score => new SmartDecision
            {
                Kind = SmartDecisionKind.Score,
                Score = (request.Options.Minimum!.Value + request.Options.Maximum!.Value) / 2,
                Provider = "sample"
            },
            SmartOperation.Validation => new SmartDecision
            {
                Kind = SmartDecisionKind.Validation,
                BooleanValue = true,
                Provider = "sample"
            },
            _ => throw new InvalidOperationException($"Unsupported operation: {request.Operation}.")
        };

        return Task.FromResult(decision);
    }
}
