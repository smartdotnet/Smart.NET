# Jev provider

`Smart.NET.Jev` adapts the official TypeSafe AI System One API to
`ISmartProvider`. Smart.NET core has no Jev or HTTP dependency; the adapter uses
the API documented at [docs.typesafe.ai](https://docs.typesafe.ai/).

## Registration

Install `Smart.NET`, `Smart.NET.Abstractions`, and `Smart.NET.Jev`, then
configure the API key from a secret store or environment variable:

```csharp
builder.Services.AddSmart();
builder.Services.AddSmartJev(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
    options.Model = "jev-latest";
});
```

`AddSmartJev` defaults to the `TYPESAFE_API_KEY`, `TYPESAFE_BASE_URL`, and
`TYPESAFE_DEFAULT_MODEL` environment variables. The API root defaults to
`https://api.typesafe.ai`; the model defaults to `jev-latest`. No credentials
are stored in source or included in logs.

## OpenRouter

To use an OpenRouter key, set `OPENROUTER_API_KEY` in the application's
environment or secret store and register the provider with
`AddSmartJevOpenRouter`:

```csharp
builder.Services.AddSmart();
builder.Services.AddSmartJevOpenRouter();
```

The helper selects the OpenRouter System One base URL
(`https://openrouter.ai/api`) and model (`typesafe/jev-1.13`). The existing
adapter then posts to `/v1/systemone` with the OpenRouter key as a Bearer token.
You may override the model using the options callback:

```csharp
builder.Services.AddSmartJevOpenRouter(options =>
{
    options.Model = "~typesafe/jev-latest";
});
```

OpenRouter also exposes the Jev Decisions API at
`https://openrouter.ai/api/alpha/decisions`; Smart.NET uses OpenRouter's
System One API instead because it preserves the TypeSafe request and response
contract already implemented by this adapter. Both surfaces use the same
OpenRouter key. See the [official OpenRouter Jev guide](https://openrouter.ai/docs/guides/community/jev)
and [System One SDK guide](https://openrouter.ai/docs/guides/community/typesafe-sdk).

To run the live OpenRouter integration test, set `OPENROUTER_API_KEY` and
explicitly enable it:

```powershell
$env:OPENROUTER_API_KEY = "your-key"
$env:SMART_OPENROUTER_INTEGRATION_TESTS = "true"
dotnet test tests\Smart.NET.IntegrationTests\Smart.NET.IntegrationTests.csproj
```

The test uses `typesafe/jev-1.13` by default; set `OPENROUTER_JEV_MODEL` to
override it. This makes a real, billable request and is disabled by default.

## Operation mapping

| Smart.NET | TypeSafe API | Mapping |
| --- | --- | --- |
| `If` and `Validate` | Noul | A probability in `[0, 1]`; Smart.NET uses `0.5` by default or the configured threshold. |
| `Switch<T>` | Choice | Allowed serialized values are sent as Choice keys, then the selected key is mapped back to the original typed value. |
| `Score` | Score | A ten-level ordered rubric spans the requested range; Jev's expected level is linearly mapped into that range. |

Jev Choice supports at most 255 options. Its Score API accepts two to ten
ordered rubric levels, so Smart.NET uses ten to preserve a continuous,
probability-weighted score over the requested range. Jev confidence is kept
separate from event probability.

## Reliability and errors

The API endpoint is `POST /v1/systemone`; the adapter sends the official
`state`, `model`, and `questions` structure and parses the typed `answers`
response. `429`, `408`, and server errors are marked transient; the core retries
them only when the caller explicitly enables a finite retry count. The adapter
honors valid `Retry-After` and `retry-after-ms` delays up to one minute.

Authentication and request-validation errors map to
`SmartConfigurationException`; transient and other provider failures map to
`SmartProviderException`; malformed or unsupported answers map to
`SmartInvalidDecisionException`. Responses and request context are never logged.

## Data and limits

Jev receives the serialized Smart.NET context as the API `state`. The official
model documentation currently lists a 64k-token context limit, up to 32k tokens
for the state plus the longest question, and dynamic rate limits (currently
listed as 1,200 requests per minute and 250,000 tokens per second). Treat all
context as potentially sensitive and untrusted; minimise and redact it before
calling Smart.NET, and keep critical risk and authorization rules deterministic.

The protocol and limits are documented at:

- [Quick start and authentication](https://docs.typesafe.ai/introduction/quickstart)
- [System One HTTP API](https://docs.typesafe.ai/api)
- [Noul](https://docs.typesafe.ai/primitives/noul)
- [Choice](https://docs.typesafe.ai/primitives/choice)
- [Score](https://docs.typesafe.ai/primitives/score)
- [Models and limits](https://docs.typesafe.ai/models)
