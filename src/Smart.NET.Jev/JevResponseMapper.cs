using System.Text.Json;
using Smart.NET.Abstractions;

namespace Smart.NET.Jev;

internal static class JevResponseMapper
{
    public static SmartDecision Map(JsonDocument response, SmartRequest request)
    {
        try
        {
            var root = response.RootElement;
            var model = GetRequiredString(root, "model");
            var answer = root.GetProperty("answers").GetProperty("decision");
            var answerType = GetRequiredString(answer, "type");
            var metadata = ReadUsageMetadata(root);

            return request.Operation switch
            {
                SmartOperation.Boolean or SmartOperation.Validation => MapNoul(answer, answerType, request, model, metadata),
                SmartOperation.Choice => MapChoice(answer, answerType, model, metadata),
                SmartOperation.Score => MapScore(answer, answerType, request, model, metadata),
                _ => throw new SmartInvalidDecisionException("Jev returned an answer for an unsupported operation.")
            };
        }
        catch (KeyNotFoundException exception)
        {
            throw new SmartInvalidDecisionException($"Jev response omitted a required field: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            throw new SmartInvalidDecisionException($"Jev response contained an invalid field: {exception.Message}");
        }
        catch (FormatException exception)
        {
            throw new SmartInvalidDecisionException($"Jev response contained an invalid number: {exception.Message}");
        }
    }

    private static SmartDecision MapNoul(
        JsonElement answer,
        string answerType,
        SmartRequest request,
        string model,
        IReadOnlyDictionary<string, object?> metadata)
    {
        if (answerType != "noul"
            || !answer.TryGetProperty("noul", out var noulValue)
            || !noulValue.TryGetDouble(out var probability)
            || !double.IsFinite(probability)
            || probability is < 0 or > 1)
        {
            throw new SmartInvalidDecisionException("Jev returned an invalid Noul answer.");
        }

        return new SmartDecision
        {
            Kind = request.Operation == SmartOperation.Validation
                ? SmartDecisionKind.Validation
                : SmartDecisionKind.Boolean,
            Probability = probability,
            Provider = "jev",
            Model = model,
            Metadata = metadata
        };
    }

    private static SmartDecision MapChoice(
        JsonElement answer,
        string answerType,
        string model,
        IReadOnlyDictionary<string, object?> metadata)
    {
        if (answerType != "choice" || !answer.TryGetProperty("choice", out var choiceValue))
        {
            throw new SmartInvalidDecisionException("Jev returned an invalid Choice answer.");
        }

        var choice = choiceValue.GetString()
            ?? throw new SmartInvalidDecisionException("Jev returned an empty Choice value.");
        var probability = GetChoiceProbability(answer, choice);

        return new SmartDecision
        {
            Kind = SmartDecisionKind.Choice,
            ChoiceValue = choice,
            Probability = probability,
            Confidence = GetRequiredProbability(answer, "confidence"),
            Provider = "jev",
            Model = model,
            Metadata = metadata
        };
    }

    private static SmartDecision MapScore(
        JsonElement answer,
        string answerType,
        SmartRequest request,
        string model,
        IReadOnlyDictionary<string, object?> metadata)
    {
        if (answerType != "score"
            || !answer.TryGetProperty("score", out var scoreValue)
            || !scoreValue.TryGetDouble(out var providerScore))
        {
            throw new SmartInvalidDecisionException("Jev returned an invalid Score answer.");
        }

        return new SmartDecision
        {
            Kind = SmartDecisionKind.Score,
            Score = JevRequestMapper.MapScore(providerScore, request),
            Confidence = GetRequiredProbability(answer, "confidence"),
            Provider = "jev",
            Model = model,
            Metadata = metadata
        };
    }

    private static IReadOnlyDictionary<string, object?> ReadUsageMetadata(JsonElement root)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (root.TryGetProperty("usage", out var usage))
        {
            AddUsageValue(metadata, usage, "input_tokens");
            AddUsageValue(metadata, usage, "output_tokens");
        }

        return metadata;
    }

    private static void AddUsageValue(
        IDictionary<string, object?> metadata,
        JsonElement usage,
        string propertyName)
    {
        if (usage.TryGetProperty(propertyName, out var value)
            && value.TryGetInt32(out var count)
            && count >= 0)
        {
            metadata[propertyName] = count;
        }
    }

    private static double GetChoiceProbability(JsonElement answer, string choice)
    {
        if (!answer.TryGetProperty("probabilities", out var probabilities)
            || !probabilities.TryGetProperty(choice, out var value))
        {
            throw new SmartInvalidDecisionException("Jev omitted the selected Choice probability.");
        }

        return ReadProbability(value, "selected Choice probability");
    }

    private static double GetRequiredProbability(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw new SmartInvalidDecisionException($"Jev omitted the required {name} value.");
        }

        return ReadProbability(value, name);
    }

    private static double ReadProbability(JsonElement value, string name)
    {
        if (!value.TryGetDouble(out var number) || !double.IsFinite(number) || number is < 0 or > 1)
        {
            throw new SmartInvalidDecisionException($"Jev returned an invalid {name}.");
        }

        return number;
    }

    private static string GetRequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.GetString() is not { Length: > 0 } text)
        {
            throw new SmartInvalidDecisionException($"Jev response omitted the required {name} value.");
        }

        return text;
    }
}
