# API notes

The public API lives in `Smart.NET` and the provider contract lives in
`Smart.NET.Abstractions`.

| API | Return | Behavior |
| --- | --- | --- |
| `ISmart.If` | `Task<bool>` | Explicit Boolean if available; otherwise probability threshold `0.5` by default. |
| `ISmart.Switch<TContext, TChoice>` | `Task<TChoice>` | Returns only an exact allowed serialized value. |
| `ISmart.Score` | `Task<double>` | Validates the provider score is finite and in the inclusive requested range. |
| `ISmart.Validate` | `Task<bool>` | Boolean validation question with the same threshold semantics as `If`. |

Each operation has a corresponding `*Result` method that returns
`SmartResult<T>` with fallback provenance, provider/model, probability,
confidence, latency, and provider metadata. Probability and confidence remain
separate fields.

`SmartOptions` can set a threshold, timeout, fallback, bounded transient retry
count, and `System.Text.Json` settings. Cancellation tokens propagate through
the provider request. Retries are off by default, only provider-marked transient
failures are eligible, and a provider-supplied retry delay is capped to a
finite supported range.

See [the architecture and API review](../architecture/overview.md) and the
[Jev adapter contract](../providers/jev.md).
