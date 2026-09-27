namespace Smart.NET.Abstractions;

/// <summary>The base class for provider-independent Smart.NET failures.</summary>
public class SmartException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SmartException"/> class.</summary>
    public SmartException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SmartException"/> class.</summary>
    /// <param name="message">A safe, provider-independent error message.</param>
    public SmartException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SmartException"/> class.</summary>
    /// <param name="message">A safe, provider-independent error message.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public SmartException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A provider-independent failure reported by a decision provider.</summary>
public sealed class SmartProviderException : SmartException
{
    /// <summary>Initializes a new instance of the <see cref="SmartProviderException"/> class.</summary>
    /// <param name="message">A safe error message that excludes secrets and request context.</param>
    /// <param name="isTransient">Whether the failure is safe to retry when retries are enabled.</param>
    /// <param name="innerException">The underlying provider exception, if any.</param>
    /// <param name="retryAfter">An optional provider-requested retry delay.</param>
    public SmartProviderException(
        string message,
        bool isTransient = false,
        Exception? innerException = null,
        TimeSpan? retryAfter = null)
        : base(message, innerException)
    {
        if (retryAfter < TimeSpan.Zero || retryAfter > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter), "Retry delay is outside the supported timer range.");
        }

        IsTransient = isTransient;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets whether the failure is transient and may be retried by an explicit policy.</summary>
    public bool IsTransient { get; }

    /// <summary>Gets the provider-requested retry delay, when supplied.</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>A decision provider failed to respond before the configured timeout.</summary>
public sealed class SmartTimeoutException : SmartException
{
    /// <summary>Initializes a new instance of the <see cref="SmartTimeoutException"/> class.</summary>
    /// <param name="timeout">The timeout that elapsed.</param>
    public SmartTimeoutException(TimeSpan timeout)
        : base($"The smart decision provider did not respond within {timeout}.")
    {
        Timeout = timeout;
    }

    /// <summary>Gets the elapsed timeout.</summary>
    public TimeSpan Timeout { get; }
}

/// <summary>A provider returned a response that violates the requested decision contract.</summary>
public sealed class SmartInvalidDecisionException : SmartException
{
    /// <summary>Initializes a new instance of the <see cref="SmartInvalidDecisionException"/> class.</summary>
    /// <param name="message">A provider-independent description of the invalid response.</param>
    public SmartInvalidDecisionException(string message)
        : base(message)
    {
    }
}

/// <summary>Smart.NET options or request context could not be used.</summary>
public sealed class SmartConfigurationException : SmartException
{
    /// <summary>Initializes a new instance of the <see cref="SmartConfigurationException"/> class.</summary>
    /// <param name="message">A safe description of the configuration failure.</param>
    public SmartConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SmartConfigurationException"/> class.</summary>
    /// <param name="message">A safe description of the configuration failure.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SmartConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
