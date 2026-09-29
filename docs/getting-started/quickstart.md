# Getting started

Smart.NET is an injected, provider-independent decision service. A provider
package implements `ISmartProvider`; application code calls `ISmart`.

## Register Jev

Set `TYPESAFE_API_KEY` using your deployment's secret manager or environment,
then register the core and provider:

```csharp
using Smart.NET.Core;
using Smart.NET.Jev;

builder.Services.AddSmart();
builder.Services.AddSmartJev();
```

Resolve `ISmart` and make decisions:

```csharp
var smart = app.Services.GetRequiredService<ISmart>();
var shouldReview = await smart.If(order, "Should this order be reviewed?");

var action = await smart.Switch(
    market,
    "What action should be considered?",
    TradingAction.Buy,
    TradingAction.Hold,
    TradingAction.Sell);

var score = await smart.Score(signal, "How strong is this signal?", 0, 100);
```

Jev returns probability-only Noul answers. Smart.NET uses `0.5` as the default
Boolean threshold; set a different threshold or a timeout/fallback explicitly:

```csharp
var result = await smart.IfResult(
    order,
    "Should this order be reviewed?",
    options => options
        .WithThreshold(0.85)
        .WithTimeout(TimeSpan.FromSeconds(1))
        .WithFallback(false));

if (result.UsedFallback)
{
    // Record that the provider did not produce this value.
}
```

Keep deterministic validation, authorization, and risk limits in application
code. Context is serialized to JSON and may leave the application when an
external provider is registered, so minimise and redact it before use.
