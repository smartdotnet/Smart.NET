# Smart.NET

[![Build](https://github.com/smartdotnet/Smart.NET/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/smartdotnet/Smart.NET/actions/workflows/build.yml)

**A provider-independent decision layer for .NET.**

Smart.NET exposes typed decision operations through `ISmart` and an
`ISmartProvider` contract. The core includes Boolean decisions, typed choices,
bounded scores, validation, timeout and cancellation support, and opt-in
fallbacks. Provider integrations are separate packages; the core does not
depend on a provider's model, HTTP API, or response format.

## Install

```sh
dotnet add package Smart.NET.Core --version 0.1.0-alpha.1
dotnet add package Smart.NET.Jev --version 0.1.0-alpha.1
```

The current `0.1.0-alpha.1` packages are prerelease software. `Smart.NET.Jev`
provides the Jev adapter; `Smart.NET.Core` provides the injected decision
service. Install `Smart.NET.Core` alone and register your own
`ISmartProvider` implementation to use a different provider.

## Example

Create a web project, set `TYPESAFE_API_KEY` in its environment or secret
manager, and use this `Program.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Smart.NET.Core;
using Smart.NET.Jev;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSmart();
builder.Services.AddSmartJev();

var app = builder.Build();
app.MapGet("/orders/{id:int}/review", async (int id, ISmart smart) =>
    await smart.If(new { OrderId = id }, "Should this order be reviewed?"));
app.Run();
```

Start the application and request `/orders/42/review` to run the example.

The same `ISmart` service supports typed choices, scores, and validation:

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

## Provider independence and safety

- Applications use `ISmart`; provider adapters implement `ISmartProvider` and
  can be changed independently of decision-handling code.
- Choices are restricted to the supplied allowed values. Scores are bounded,
  not implicitly calibrated probabilities. Fallbacks are opt-in.
- Context is serialized to JSON and may be sent to an external provider.
  Minimise and redact sensitive data before sending it.
- Smart.NET suggests decisions; it is not an authorization system or a
  deterministic risk control. Keep hard safety, compliance, and execution
  constraints in application logic.

See the [quickstart](https://github.com/smartdotnet/Smart.NET/blob/master/docs/getting-started/quickstart.md),
[Jev provider guide](https://github.com/smartdotnet/Smart.NET/blob/master/docs/providers/jev.md),
and [release instructions](https://github.com/smartdotnet/Smart.NET/blob/master/docs/releasing.md).
Source code and issue tracking are on
[GitHub](https://github.com/smartdotnet/Smart.NET).
