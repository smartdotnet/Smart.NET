namespace Smart.NET.Abstractions;

/// <summary>Executes provider-independent smart decision requests.</summary>
public interface ISmartProvider
{
    /// <summary>Returns the provider's response for a structured decision request.</summary>
    /// <param name="request">The decision operation, question, serialized context, and constraints.</param>
    /// <param name="cancellationToken">Token used to cancel the provider operation.</param>
    /// <returns>A provider-independent decision.</returns>
    Task<SmartDecision> DecideAsync(
        SmartRequest request,
        CancellationToken cancellationToken = default);
}
