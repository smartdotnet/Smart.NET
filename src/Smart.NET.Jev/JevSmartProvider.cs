using Smart.NET.Abstractions;

namespace Smart.NET.Jev;

/// <summary>Adapts the official TypeSafe AI Jev API to <see cref="ISmartProvider"/>.</summary>
public sealed class JevSmartProvider : ISmartProvider
{
    private readonly JevHttpClient _client;

    /// <summary>Creates a provider backed by the configured Jev HTTP client.</summary>
    /// <param name="client">The factory-managed Jev HTTP client.</param>
    public JevSmartProvider(JevHttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public async Task<SmartDecision> DecideAsync(
        SmartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = JevRequestMapper.Map(request, _client.Model);
        using var response = await _client.DecideAsync(payload, cancellationToken).ConfigureAwait(false);
        return JevResponseMapper.Map(response, request);
    }
}
