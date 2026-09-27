using System.Text.Json;

namespace Smart.NET.Abstractions;

/// <summary>A provider-independent request for one smart operation.</summary>
public sealed record SmartRequest
{
    /// <summary>Gets the requested operation.</summary>
    public required SmartOperation Operation { get; init; }

    /// <summary>Gets the natural-language decision question.</summary>
    public required string Question { get; init; }

    /// <summary>Gets the serialized application context, if supplied.</summary>
    public JsonElement? Context { get; init; }

    /// <summary>Gets the choices available to a categorical decision.</summary>
    public IReadOnlyList<SmartOption>? Choices { get; init; }

    /// <summary>Gets operation-specific constraints.</summary>
    public SmartRequestOptions Options { get; init; } = new();
}

/// <summary>An allowed categorical decision value and its human-readable label.</summary>
/// <param name="Value">The serialized value to return when this choice is selected.</param>
/// <param name="Label">A readable name for the choice.</param>
public sealed record SmartOption(string Value, string Label);

/// <summary>Constraints passed to a provider for a single operation.</summary>
public sealed record SmartRequestOptions
{
    /// <summary>Gets a probability threshold for an explicitly probabilistic Boolean decision.</summary>
    public double? Threshold { get; init; }

    /// <summary>Gets the minimum allowed score.</summary>
    public double? Minimum { get; init; }

    /// <summary>Gets the maximum allowed score.</summary>
    public double? Maximum { get; init; }
}
