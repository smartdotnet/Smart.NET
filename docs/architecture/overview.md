# Smart.NET architecture and API review

## Proposed architecture

```text
Application
    ├── Smart.NET.Core
    │       └── Smart.NET.Abstractions
    └── optional provider package (for example, Smart.NET.Jev)
                └── Smart.NET.Abstractions
```

- **Smart.NET.Abstractions** owns `ISmartProvider`, `SmartRequest`,
  `SmartDecision`, provider-neutral enums, and result/error contracts.
- **Smart.NET.Core** owns the injected `ISmart` API, request validation, context
  serialization, decision mapping, reliability policies, diagnostics, and
  dependency-injection registration.
- **Smart.NET.Jev** will be an optional adapter. It may depend on the
  abstractions, but the core packages must never depend on it.
- Unit tests use an in-memory provider and make no network calls. Provider
  contract tests exercise only the provider-neutral request/response contract.

The canonical API is injected `ISmart`, not a static `Smart` facade. A static
facade would need process-wide mutable provider state or hidden service
resolution; either makes isolated configuration and tests harder. Applications
can still use the concise `await smart.If(...)` form after resolving `ISmart`.

## Public API proposal

```csharp
public interface ISmart
{
    Task<bool> If<TContext>(
        TContext context, string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<SmartResult<bool>> IfResult<TContext>(
        TContext context, string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<TChoice> Switch<TContext, TChoice>(
        TContext context, string question, params TChoice[] choices);

    Task<TChoice> Switch<TContext, TChoice>(
        TContext context, string question, IReadOnlyList<TChoice> choices,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<double> Score<TContext>(
        TContext context, string question,
        double minimum = 0, double maximum = 1,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<bool> Validate<TContext>(
        TContext context, string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);
}

public interface ISmartProvider
{
    Task<SmartDecision> DecideAsync(
        SmartRequest request,
        CancellationToken cancellationToken = default);
}
```

The result-returning forms preserve whether a configured fallback was used;
convenience forms return only the value. A probability is metadata, not a
confidence score. Boolean decisions use the provider's explicit Boolean value
when present; probability-only Boolean providers use the documented default
threshold of 0.5, which callers can override. Scores are bounded numeric
outputs, not implicitly calibrated probabilities.

## Usage examples

```csharp
var review = await smart.If(order, "Should this order be reviewed?");

var action = await smart.Switch(
    market, "What action should be considered?",
    TradeAction.Buy, TradeAction.Hold, TradeAction.Sell);

var score = await smart.Score(
    signal, "How strong is this signal?", 0, 100);
```

`Switch` sends a finite list of allowed serialized values and maps only an
exact match back to the original typed value. `Validate` is a Boolean decision
in v0.1; structured issue reporting is deferred.

## Provider architecture

Jev, future OpenAI/local adapters, and test fakes all implement
`ISmartProvider`. They receive the same structured operation, question,
serialized context, options, and allowed choices, then return a
provider-independent `SmartDecision`. Only an adapter knows its provider's
HTTP, authentication, model, and response schema. No provider API is assumed
in the core.

## API risks and decisions

| Concern | Decision |
| --- | --- |
| Static facade vs DI | Use injected `ISmart`; do not introduce global mutable state. |
| Generic choices | Preserve the caller's type and reject choices not in the exact allowed set. |
| Enums | Serialize allowed values using the configured `System.Text.Json` options; return the original enum value. |
| Probability semantics | Never infer a Boolean from probability unless the caller explicitly supplies a threshold. |
| Fallback | Keep fallback opt-in and expose `UsedFallback` in `SmartResult<T>`. |
| Context serialization | Serialize once with `System.Text.Json`; provider code receives the JSON value, not an application object. |
| Timeout/cancellation | Link timeout and caller cancellation; caller cancellation propagates, timeout has a provider-independent exception. |
| Retry | Disabled by default; only retry explicitly enabled, provider-marked transient failures, with a finite limit. |
| Result types | Keep convenience values while offering result forms for fallback/provenance metadata. |
| Naming | Use `If`, `Switch`, `Score`, and `Validate`; provider vocabulary stays in adapters. |

The Jev adapter is isolated in `Smart.NET.Jev` and implements the verified
TypeSafe System One HTTP contract. Its provider selection, authentication, JSON
mapping, status mapping, and retry-after handling do not enter the core
packages.
