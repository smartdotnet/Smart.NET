# Smart.NET

**A provider-independent decision layer for .NET.**

Ask a decision question using familiar C# primitives:

```csharp
if (await smart.If(order, "Should this order be reviewed?"))
{
    SendToReview(order);
}
```

Use the same injected `ISmart` service for typed choices, scores, and
validation:

```csharp
var action = await smart.Switch(
    market,
    "What action should be considered?",
    TradingAction.Buy,
    TradingAction.Hold,
    TradingAction.Sell);

var strength = await smart.Score(signal, "How strong is this signal?", 0, 100);
var isConsistent = await smart.Validate(trade, "Is this trade internally consistent?");
```

Smart.NET defines provider-independent operations and contracts. Applications
depend on `ISmart`, not on a provider's model, HTTP API, or response format.

## Status

This repository is an early `0.1.0-alpha.1` implementation. It includes the
provider-neutral core, an in-memory sample provider, and a Jev adapter built
against the current official API. The public API remains provider-independent.

## Getting started

Install `Smart.NET`, `Smart.NET.Abstractions`, and the provider package you
want to use. For Jev:

```csharp
using Smart.NET;
using Smart.NET.Jev;

builder.Services.AddSmart();
builder.Services.AddSmartJev();

var smart = app.Services.GetRequiredService<ISmart>();
var shouldReview = await smart.If(order, "Should this order be reviewed?");
```

For custom providers, register an `ISmartProvider` implementation in place of
`AddSmartJev`. See [Jev provider configuration](docs/providers/jev.md).

The package IDs are `Smart.NET`, `Smart.NET.Abstractions`, and `Smart.NET.Jev`.
Packages are not published yet; project references are used in this repository.
Once published, install them with:

```powershell
dotnet add package Smart.NET --version 0.1.0-alpha.1
dotnet add package Smart.NET.Jev --version 0.1.0-alpha.1
```

The console sample uses a deterministic local provider and runs without network
access:

```powershell
dotnet run --project samples\Smart.NET.ConsoleSample
```

The ASP.NET Core sample demonstrates resolving `ISmart` through request-scoped
dependency injection; its in-memory provider is educational, not a production
decision engine:

```powershell
dotnet run --project samples\Smart.NET.AspNetSample
```

## Decisions, reliability, and safety

- Boolean decisions use an explicit Boolean value when the provider returns
  one. For probability-only providers, Smart.NET uses a documented default
  threshold of `0.5`, configurable with `options.WithThreshold(0.85)`.
- Scores are bounded values, not implicitly calibrated probabilities.
- `Switch` sends the allowed choice set and rejects a response that is not an
  exact allowed value. The returned value retains its original .NET type,
  including enum types.
- Fallbacks are opt-in. Use `IfResult`, `SwitchResult`, `ScoreResult`, or
  `ValidateResult` to inspect `UsedFallback`, provider, probability, and
  latency.
- Timeouts and cancellation are supported. Retries are disabled by default and
  apply only to provider failures explicitly marked transient.
- Application context is serialized to JSON before being passed to a provider.
  It may contain sensitive or user-controlled data: minimise it, redact it
  before calling Smart.NET, and treat it as untrusted input. The core does not
  log request context or credentials.
- Smart.NET makes decisions; it is not an authorization system or a
  deterministic risk control. Keep hard safety, compliance, and execution
  constraints in application logic.

## Development

```powershell
dotnet test Smart.NET.sln
dotnet pack src\Smart.NET.Abstractions\Smart.NET.Abstractions.csproj --configuration Release --output artifacts
dotnet pack src\Smart.NET\Smart.NET.csproj --configuration Release --output artifacts
dotnet pack src\Smart.NET.Jev\Smart.NET.Jev.csproj --configuration Release --output artifacts
```

Packages use semantic versioning and prerelease versions until the public API
is reviewed. See [the architecture and API review](docs/architecture/overview.md).
