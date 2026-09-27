using System.Globalization;
using System.Text.Json;
using Smart.NET.Abstractions;

namespace Smart.NET.Jev;

internal static class JevRequestMapper
{
    private const int ScoreLevelCount = 10;

    public static JevApiRequest Map(SmartRequest request, string model)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Context is not { } state
            || state.ValueKind is not (JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null))
        {
            throw new SmartConfigurationException(
                "Jev state must be a string, JSON object, array, or null value.");
        }

        var jevQuestion = request.Operation switch
        {
            SmartOperation.Boolean or SmartOperation.Validation => new JevQuestion
            {
                Type = "noul",
                Instructions = request.Question
            },
            SmartOperation.Choice => CreateChoiceQuestion(request),
            SmartOperation.Score => CreateScoreQuestion(request),
            _ => throw new SmartConfigurationException($"The Jev provider does not support {request.Operation}.")
        };

        return new JevApiRequest
        {
            State = state,
            Model = model,
            Questions = new Dictionary<string, JevQuestion>(StringComparer.Ordinal)
            {
                ["decision"] = jevQuestion
            }
        };
    }

    private static JevQuestion CreateChoiceQuestion(SmartRequest request)
    {
        if (request.Choices is not { Count: > 0 and <= 255 } choices)
        {
            throw new SmartConfigurationException("Jev Choice requires between one and 255 allowed choices.");
        }

        var criteria = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            if (!criteria.TryAdd(choice.Value, choice.Label))
            {
                throw new SmartConfigurationException("Jev Choice values must be unique.");
            }
        }

        return new JevQuestion
        {
            Type = "choice",
            Instructions = request.Question,
            Criteria = criteria
        };
    }

    private static JevQuestion CreateScoreQuestion(SmartRequest request)
    {
        if (request.Options.Minimum is not { } minimum
            || request.Options.Maximum is not { } maximum
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || minimum >= maximum)
        {
            throw new SmartConfigurationException("Jev Score requires finite, increasing score bounds.");
        }

        var levels = new string[ScoreLevelCount];
        for (var index = 0; index < levels.Length; index++)
        {
            var fraction = (double)index / (levels.Length - 1);
            var value = minimum * (1 - fraction) + maximum * fraction;
            levels[index] = value.ToString("G17", CultureInfo.InvariantCulture);
        }

        return new JevQuestion
        {
            Type = "score",
            Instructions = request.Question,
            Criteria = levels
        };
    }

    public static double MapScore(double providerScore, SmartRequest request)
    {
        if (!double.IsFinite(providerScore)
            || providerScore is < 0 or > ScoreLevelCount - 1
            || request.Options.Minimum is not { } minimum
            || request.Options.Maximum is not { } maximum)
        {
            throw new SmartInvalidDecisionException("Jev returned a score outside the supported rubric.");
        }

        var fraction = providerScore / (ScoreLevelCount - 1);
        return Math.Clamp(minimum * (1 - fraction) + maximum * fraction, minimum, maximum);
    }
}
