using Smart.NET.Core;
using Smart.NET.Abstractions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ISmartProvider, DemoProvider>();
builder.Services.AddSmart();

var app = builder.Build();

app.MapGet("/orders/{id:int}/review", async (int id, ISmart smart, CancellationToken cancellationToken) =>
{
    var result = await smart.IfResult(
        new { OrderId = id },
        "Should this order be manually reviewed?",
        options => options.WithTimeout(TimeSpan.FromSeconds(1)).WithFallback(false),
        cancellationToken);

    return Results.Ok(new { result.Value, result.UsedFallback, result.Provider });
});

app.Run();

internal sealed class DemoProvider : ISmartProvider
{
    public Task<SmartDecision> DecideAsync(
        SmartRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Operation != SmartOperation.Boolean)
        {
            throw new SmartProviderException("The sample provider supports Boolean decisions only.");
        }

        return Task.FromResult(new SmartDecision
        {
            Kind = SmartDecisionKind.Boolean,
            BooleanValue = true,
            Provider = "sample"
        });
    }
}
