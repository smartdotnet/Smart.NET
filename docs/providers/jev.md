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
