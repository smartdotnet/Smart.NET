using System.Text.Json;

namespace Smart.NET.Core;

/// <summary>Configures one smart decision without changing shared service state.</summary>
public sealed class SmartOptions
{
    private static readonly TimeSpan MaximumTimerDuration = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>Gets the optional threshold for probability-based Boolean conversion.</summary>
    public double? Threshold { get; private set; }

    /// <summary>Gets the optional provider timeout.</summary>
    public TimeSpan? Timeout { get; private set; }

    /// <summary>Gets whether a fallback value has been explicitly configured.</summary>
    public bool HasFallback { get; private set; }

    /// <summary>Gets the configured fallback value.</summary>
    public object? Fallback { get; private set; }

    /// <summary>Gets the maximum number of transient retries; the default is zero.</summary>
    public int MaxRetries { get; private set; }

    /// <summary>Gets the delay between explicitly enabled retries.</summary>
    public TimeSpan RetryDelay { get; private set; }

    /// <summary>Gets the serializer settings used for application context and choices.</summary>
    public JsonSerializerOptions JsonSerializerOptions { get; private set; } = new();

    /// <summary>Sets the probability threshold used when converting a probability to a Boolean.</summary>
    /// <param name="threshold">A finite probability between zero and one, inclusive.</param>
    /// <returns>This options instance.</returns>
    public SmartOptions WithThreshold(double threshold)
    {
        if (!double.IsFinite(threshold) || threshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between zero and one.");
        }

        Threshold = threshold;
        return this;
    }

    /// <summary>Sets a finite timeout for the provider operation.</summary>
    /// <param name="timeout">A positive timeout.</param>
    /// <returns>This options instance.</returns>
    public SmartOptions WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > MaximumTimerDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Timeout must be positive and no longer than the supported timer duration.");
        }

        Timeout = timeout;
        return this;
    }

    /// <summary>Sets a value to return only when a provider or decision failure occurs.</summary>
    /// <typeparam name="T">The decision value type.</typeparam>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>This options instance.</returns>
    public SmartOptions WithFallback<T>(T fallback)
    {
        Fallback = fallback;
        HasFallback = true;
        return this;
    }

    /// <summary>Enables a finite number of retries for provider-marked transient failures.</summary>
    /// <param name="maxRetries">The maximum retries after the initial attempt.</param>
    /// <param name="delay">The delay between attempts; defaults to no delay.</param>
    /// <returns>This options instance.</returns>
    public SmartOptions WithRetries(int maxRetries, TimeSpan? delay = null)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Retry count cannot be negative.");
        }

        if (delay < TimeSpan.Zero || delay > MaximumTimerDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                "Retry delay cannot be negative or exceed the supported timer duration.");
        }

        MaxRetries = maxRetries;
        RetryDelay = delay ?? TimeSpan.Zero;
        return this;
    }

    /// <summary>Sets the JSON serializer settings used for application context and choices.</summary>
    /// <param name="jsonSerializerOptions">The serializer settings to use.</param>
    /// <returns>This options instance.</returns>
    public SmartOptions WithJsonSerializerOptions(JsonSerializerOptions jsonSerializerOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        JsonSerializerOptions = jsonSerializerOptions;
        return this;
    }
}
