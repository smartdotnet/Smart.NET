namespace Smart.NET.Abstractions;

/// <summary>A decision value together with provider and fallback provenance.</summary>
/// <typeparam name="T">The strongly typed decision value.</typeparam>
public sealed record SmartResult<T>
{
    /// <summary>Gets the decision value or configured fallback.</summary>
    public required T Value { get; init; }

    /// <summary>Gets whether the value came from configured fallback behavior.</summary>
    public bool UsedFallback { get; init; }

    /// <summary>Gets the provider identifier, when available.</summary>
    public string? Provider { get; init; }

    /// <summary>Gets the provider model identifier, when available.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the provider-reported probability, when available.</summary>
    public double? Probability { get; init; }

    /// <summary>Gets the provider-reported confidence, when available.</summary>
    public double? Confidence { get; init; }

    /// <summary>Gets elapsed wall-clock time for the operation.</summary>
    public TimeSpan? Latency { get; init; }

    /// <summary>Gets provider metadata that does not contain application context or credentials.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();
}
