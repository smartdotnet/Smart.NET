using System.Diagnostics;

namespace Smart.NET.Core;

/// <summary>Diagnostic instrumentation exposed by the Smart.NET.Core package.</summary>
public static class SmartDiagnostics
{
    /// <summary>Gets the activity source used for provider-independent decision operations.</summary>
    public static ActivitySource ActivitySource { get; } = new("Smart.NET.Core");
}
