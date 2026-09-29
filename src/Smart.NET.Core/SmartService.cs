using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Smart.NET.Abstractions;

namespace Smart.NET.Core;

/// <summary>Executes smart operations using one configured provider.</summary>
public sealed class SmartService : ISmart
{
    private readonly ISmartProvider _provider;
    private readonly ILogger<SmartService>? _logger;

    /// <summary>Creates a service backed by the registered provider.</summary>
    /// <param name="provider">The provider used to execute requests.</param>
    /// <param name="logger">An optional logger. Request context and secrets are never logged.</param>
    public SmartService(ISmartProvider provider, ILogger<SmartService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> If<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return (await IfResult(context, question, configure, cancellationToken).ConfigureAwait(false)).Value;
    }

    /// <inheritdoc />
    public Task<SmartResult<bool>> IfResult<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = CreateOptions(configure);
        EnsureFallback<bool>(options);
        var request = CreateRequest(
            SmartOperation.Boolean,
            context,
            question,
            options,
            requestOptions: new SmartRequestOptions { Threshold = options.Threshold });
        return ExecuteAsync(
            request,
            options,
            cancellationToken,
            decision => MapBoolean(decision, SmartDecisionKind.Boolean, options.Threshold));
    }

    /// <inheritdoc />
    public Task<TChoice> Switch<TContext, TChoice>(
        TContext context,
        string question,
        params TChoice[] choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return Switch(context, question, (IReadOnlyList<TChoice>)choices);
    }

    /// <inheritdoc />
    public async Task<TChoice> Switch<TContext, TChoice>(
        TContext context,
        string question,
        IReadOnlyList<TChoice> choices,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return (await SwitchResult(context, question, choices, configure, cancellationToken)
            .ConfigureAwait(false)).Value;
    }

    /// <inheritdoc />
    public Task<SmartResult<TChoice>> SwitchResult<TContext, TChoice>(
        TContext context,
        string question,
        IReadOnlyList<TChoice> choices,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.Count == 0)
        {
            throw new ArgumentException("At least one allowed choice is required.", nameof(choices));
        }

        var options = CreateOptions(configure);
        EnsureThresholdIsBooleanOnly(options);
        var allowedChoices = new Dictionary<string, TChoice>(StringComparer.Ordinal);
        var requestChoices = new List<SmartOption>(choices.Count);

        foreach (var choice in choices)
        {
            var value = SerializeChoice(choice, options.JsonSerializerOptions);
            if (!allowedChoices.TryAdd(value, choice))
            {
                throw new ArgumentException("Allowed choices must have unique serialized values.", nameof(choices));
            }

            var label = Convert.ToString(choice, CultureInfo.InvariantCulture) ?? "null";
            requestChoices.Add(new SmartOption(value, label));
        }

        if (options.HasFallback)
        {
            if (options.Fallback is TChoice fallbackChoice)
            {
                if (!allowedChoices.ContainsKey(SerializeChoice(fallbackChoice, options.JsonSerializerOptions)))
                {
                    throw new SmartConfigurationException("The configured fallback must be one of the allowed choices.");
                }
            }
            else if (options.Fallback is not null
                || default(TChoice) is not null
                || !allowedChoices.ContainsKey("null"))
            {
                throw new SmartConfigurationException("The configured fallback must be one of the allowed choices.");
            }
        }

        var request = CreateRequest(
            SmartOperation.Choice,
            context,
            question,
            options,
            requestChoices: requestChoices);

        return ExecuteAsync(
            request,
            options,
            cancellationToken,
            decision => MapChoice(decision, allowedChoices));
    }

    /// <inheritdoc />
    public async Task<double> Score<TContext>(
        TContext context,
        string question,
        double minimum = 0,
        double maximum = 1,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return (await ScoreResult(context, question, minimum, maximum, configure, cancellationToken)
            .ConfigureAwait(false)).Value;
    }

    /// <inheritdoc />
    public Task<SmartResult<double>> ScoreResult<TContext>(
        TContext context,
        string question,
        double minimum = 0,
        double maximum = 1,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Score bounds must be finite and minimum must be less than maximum.");
        }

        var options = CreateOptions(configure);
        EnsureThresholdIsBooleanOnly(options);
        if (options.HasFallback
            && (!TryGetFallback(options, out double fallback)
                || !double.IsFinite(fallback)
                || fallback < minimum
                || fallback > maximum))
        {
            throw new SmartConfigurationException("The configured fallback must be a finite score within the requested range.");
        }

        var request = CreateRequest(
            SmartOperation.Score,
            context,
            question,
            options,
            requestOptions: new SmartRequestOptions { Minimum = minimum, Maximum = maximum });
        return ExecuteAsync(
            request,
            options,
            cancellationToken,
            decision => MapScore(decision, minimum, maximum));
    }

    /// <inheritdoc />
    public async Task<bool> Validate<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return (await ValidateResult(context, question, configure, cancellationToken).ConfigureAwait(false)).Value;
    }

    /// <inheritdoc />
    public Task<SmartResult<bool>> ValidateResult<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = CreateOptions(configure);
        EnsureFallback<bool>(options);
        var request = CreateRequest(
            SmartOperation.Validation,
            context,
            question,
            options,
            requestOptions: new SmartRequestOptions { Threshold = options.Threshold });
        return ExecuteAsync(
            request,
            options,
            cancellationToken,
            decision => MapBoolean(decision, SmartDecisionKind.Validation, options.Threshold));
    }

    private static SmartOptions CreateOptions(Action<SmartOptions>? configure)
    {
        var options = new SmartOptions();
        configure?.Invoke(options);
        return options;
    }

    private static SmartRequest CreateRequest<TContext>(
        SmartOperation operation,
        TContext context,
        string question,
        SmartOptions options,
        SmartRequestOptions? requestOptions = null,
        IReadOnlyList<SmartOption>? requestChoices = null)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("A non-empty question is required.", nameof(question));
        }

        JsonElement serializedContext;
        try
        {
            serializedContext = JsonSerializer.SerializeToElement(context, options.JsonSerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new SmartConfigurationException("The decision context could not be serialized.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new SmartConfigurationException("The decision context could not be serialized.", exception);
        }

        return new SmartRequest
        {
            Operation = operation,
            Question = question,
            Context = serializedContext,
            Choices = requestChoices,
            Options = requestOptions ?? new SmartRequestOptions()
        };
    }

    private async Task<SmartResult<T>> ExecuteAsync<T>(
        SmartRequest request,
        SmartOptions options,
        CancellationToken cancellationToken,
        Func<SmartDecision, T> mapDecision)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = SmartDiagnostics.ActivitySource.StartActivity(request.Operation.ToString());
        activity?.SetTag("smart.operation", request.Operation.ToString());

        try
        {
            var decision = await RequestDecisionAsync(request, options, cancellationToken).ConfigureAwait(false);
            if (decision is null)
            {
                throw new SmartInvalidDecisionException("The provider returned no decision.");
            }

            ValidateProbability(decision.Probability);
            ValidateProbability(decision.Confidence);
            if (decision.Metadata is null)
            {
                throw new SmartInvalidDecisionException("The provider returned invalid metadata.");
            }

            var value = mapDecision(decision);
            var latency = decision.Latency ?? Stopwatch.GetElapsedTime(startedAt);
            if (latency < TimeSpan.Zero)
            {
                throw new SmartInvalidDecisionException("The provider returned a negative latency.");
            }

            activity?.SetTag("smart.provider", decision.Provider);
            activity?.SetTag("smart.model", decision.Model);
            activity?.SetTag("smart.duration", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            activity?.SetTag("smart.success", true);
            _logger?.LogDebug("Smart operation {Operation} completed.", request.Operation);
            return new SmartResult<T>
            {
                Value = value,
                Provider = decision.Provider,
                Model = decision.Model,
                Probability = decision.Probability,
                Confidence = decision.Confidence,
                Latency = latency,
                Metadata = decision.Metadata
            };
        }
        catch (SmartProviderException) when (options.HasFallback)
        {
            return UseFallback<T>(options, startedAt, request.Operation, activity);
        }
        catch (SmartTimeoutException) when (options.HasFallback)
        {
            return UseFallback<T>(options, startedAt, request.Operation, activity);
        }
        catch (SmartInvalidDecisionException) when (options.HasFallback)
        {
            return UseFallback<T>(options, startedAt, request.Operation, activity);
        }
        catch (SmartException exception)
        {
            RecordFailure(activity, startedAt, request.Operation, exception.GetType().Name);
            _logger?.LogWarning(
                "Smart operation {Operation} failed with {FailureType}.",
                request.Operation,
                exception.GetType().Name);
            throw;
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("smart.cancelled", true);
            activity?.SetTag("smart.duration", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    private async Task<SmartDecision> RequestDecisionAsync(
        SmartRequest request,
        SmartOptions options,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var providerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (options.Timeout is { } timeoutDuration)
            {
                providerCancellation.CancelAfter(timeoutDuration);
            }

            try
            {
                var providerTask = _provider.DecideAsync(request, providerCancellation.Token);
                if (providerTask is null)
                {
                    throw new SmartProviderException("The configured provider returned no operation.");
                }

                if (options.Timeout is { } providerTimeout)
                {
                    return await providerTask.WaitAsync(providerTimeout, cancellationToken).ConfigureAwait(false);
                }

                return await providerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException) when (options.Timeout is { } elapsedTimeout)
            {
                providerCancellation.Cancel();
                throw new SmartTimeoutException(elapsedTimeout);
            }
            catch (OperationCanceledException) when (
                options.Timeout is { } canceledTimeout && providerCancellation.IsCancellationRequested)
            {
                throw new SmartTimeoutException(canceledTimeout);
            }
            catch (SmartProviderException exception) when (
                exception.IsTransient && attempt < options.MaxRetries)
            {
                var retryDelay = exception.RetryAfter ?? options.RetryDelay;
                if (retryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SmartException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SmartProviderException("The configured provider failed.", innerException: exception);
            }
        }
    }

    private SmartResult<T> UseFallback<T>(
        SmartOptions options,
        long startedAt,
        SmartOperation operation,
        Activity? activity)
    {
        if (!TryGetFallback(options, out T? value))
        {
            throw new SmartConfigurationException(
                $"The configured fallback is not a valid value for the {operation} operation.");
        }

        activity?.SetTag("smart.success", true);
        activity?.SetTag("smart.fallback", true);
        activity?.SetTag("smart.failure", true);
        activity?.SetTag("smart.duration", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        _logger?.LogWarning("Smart operation {Operation} used its configured fallback.", operation);
        return new SmartResult<T>
        {
            Value = value!,
            UsedFallback = true,
            Latency = Stopwatch.GetElapsedTime(startedAt)
        };
    }

    private static void RecordFailure(
        Activity? activity,
        long startedAt,
        SmartOperation operation,
        string failureType)
    {
        activity?.SetTag("smart.operation", operation.ToString());
        activity?.SetTag("smart.failure", true);
        activity?.SetTag("smart.failure_type", failureType);
        activity?.SetTag("smart.duration", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Error, failureType);
    }

    private static bool TryGetFallback<T>(SmartOptions options, out T? value)
    {
        if (options.Fallback is T typedValue)
        {
            value = typedValue;
            return true;
        }

        if (options.Fallback is null && default(T) is null)
        {
            value = default;
            return true;
        }

        value = default;
        return false;
    }

    private static void EnsureFallback<T>(SmartOptions options)
    {
        if (options.HasFallback && !TryGetFallback<T>(options, out _))
        {
            throw new SmartConfigurationException($"The configured fallback is not a valid {typeof(T).Name} value.");
        }
    }

    private static void EnsureThresholdIsBooleanOnly(SmartOptions options)
    {
        if (options.Threshold is not null)
        {
            throw new SmartConfigurationException("A probability threshold is only valid for Boolean decisions.");
        }
    }

    private static bool MapBoolean(
        SmartDecision decision,
        SmartDecisionKind expectedKind,
        double? threshold)
    {
        if (decision.Kind != expectedKind)
        {
            throw new SmartInvalidDecisionException($"The provider returned {decision.Kind} for a {expectedKind} operation.");
        }

        if (threshold is { } configuredThreshold && decision.Probability is { } thresholdProbability)
        {
            return thresholdProbability >= configuredThreshold;
        }

        if (decision.BooleanValue is { } booleanValue)
        {
            return booleanValue;
        }

        if (decision.Probability is { } probabilityValue)
        {
            return probabilityValue >= (threshold ?? 0.5);
        }

        throw new SmartInvalidDecisionException("The provider returned neither a Boolean value nor a probability.");
    }

    private static TChoice MapChoice<TChoice>(
        SmartDecision decision,
        IReadOnlyDictionary<string, TChoice> allowedChoices)
    {
        if (decision.Kind != SmartDecisionKind.Choice || decision.ChoiceValue is null)
        {
            throw new SmartInvalidDecisionException("The provider returned no categorical choice.");
        }

        if (allowedChoices.TryGetValue(decision.ChoiceValue, out var choice))
        {
            return choice;
        }

        throw new SmartInvalidDecisionException("The provider returned a choice outside the allowed set.");
    }

    private static double MapScore(SmartDecision decision, double minimum, double maximum)
    {
        if (decision.Kind != SmartDecisionKind.Score
            || decision.Score is not { } score
            || !double.IsFinite(score)
            || score < minimum
            || score > maximum)
        {
            throw new SmartInvalidDecisionException("The provider returned an invalid score or a score outside the requested range.");
        }

        return score;
    }

    private static string SerializeChoice<TChoice>(TChoice choice, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Serialize(choice, options);
        }
        catch (JsonException exception)
        {
            throw new SmartConfigurationException("An allowed choice could not be serialized.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new SmartConfigurationException("An allowed choice could not be serialized.", exception);
        }
    }

    private static void ValidateProbability(double? probability)
    {
        if (probability is { } value && (!double.IsFinite(value) || value is < 0 or > 1))
        {
            throw new SmartInvalidDecisionException("The provider returned an invalid probability.");
        }
    }
}
