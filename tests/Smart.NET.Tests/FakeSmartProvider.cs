using Smart.NET.Abstractions;

namespace Smart.NET.Tests;

internal sealed class FakeSmartProvider : ISmartProvider
{
    private readonly Dictionary<string, Func<SmartRequest, CancellationToken, Task<SmartDecision>>> _responses =
        new(StringComparer.Ordinal);

    public List<SmartRequest> Requests { get; } = [];

    public int CallCount { get; private set; }

    public FakeSmartProvider When(string question, SmartDecision decision)
    {
        _responses.Add(question, (_, _) => Task.FromResult(decision));
        return this;
    }

    public FakeSmartProvider When(
        string question,
        Func<SmartRequest, CancellationToken, Task<SmartDecision>> response)
    {
        _responses.Add(question, response);
        return this;
    }

    public Task<SmartDecision> DecideAsync(
        SmartRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Requests.Add(request);
        if (!_responses.TryGetValue(request.Question, out var response))
        {
            throw new SmartProviderException("No fake response was configured.");
        }

        return response(request, cancellationToken);
    }
}
