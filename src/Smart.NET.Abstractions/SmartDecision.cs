namespace Smart.NET.Abstractions;

/// <summary>The provider-independent decision response.</summary>
public sealed record SmartDecision
{
    /// <summary>Gets the shape of this decision.</summary>
    public required SmartDecisionKind Kind { get; init; }

    /// <summary>Gets the explicit Boolean answer, when applicable.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected serialized choice, when applicable.</summary>
    public string? ChoiceValue { get; init; }

    /// <summary>Gets the numeric score, when applicable.</summary>
    public double? Score { get; init; }

    /// <summary>Gets the provider-reported probability, when available.</summary>
    public double? Probability { get; init; }

    /// <summary>Gets the provider-reported confidence, when available.</summary>
    public double? Confidence { get; init; }

    /// <summary>Gets the provider-neutral provider identifier.</summary>
    public string? Provider { get; init; }

    /// <summary>Gets the elapsed provider latency, when reported.</summary>
    public TimeSpan? Latency { get; init; }

    /// <summary>Gets the model identifier, when available.</summary>
    public string? Model { get; init; }

    /// <summary>Gets non-sensitive provider metadata.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();
}

/// <summary>The operation requested from a provider.</summary>
public enum SmartOperation
{
    /// <summary>Produce a Boolean decision.</summary>
    Boolean,

    /// <summary>Select one of the allowed categorical choices.</summary>
    Choice,

    /// <summary>Produce a bounded numeric score.</summary>
    Score,

    /// <summary>Validate whether the supplied context satisfies the question.</summary>
    Validation
}

/// <summary>The shape of the response returned by a provider.</summary>
public enum SmartDecisionKind
{
    /// <summary>The response contains an explicit Boolean decision.</summary>
    Boolean,

    /// <summary>The response contains a selected choice.</summary>
    Choice,

    /// <summary>The response contains a numeric score.</summary>
    Score,

    /// <summary>The response contains a validation Boolean.</summary>
    Validation
}
