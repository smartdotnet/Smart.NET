using Smart.NET.Abstractions;

namespace Smart.NET;

/// <summary>Runs provider-independent smart decisions through dependency injection.</summary>
public interface ISmart
{
    /// <summary>Returns an explicit Boolean decision.</summary>
    Task<bool> If<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a Boolean decision with fallback and provider provenance.</summary>
    Task<SmartResult<bool>> IfResult<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one of the supplied allowed choices.</summary>
    Task<TChoice> Switch<TContext, TChoice>(
        TContext context,
        string question,
        params TChoice[] choices);

    /// <summary>Returns one of the supplied allowed choices with operation options.</summary>
    Task<TChoice> Switch<TContext, TChoice>(
        TContext context,
        string question,
        IReadOnlyList<TChoice> choices,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a typed choice with fallback and provider provenance.</summary>
    Task<SmartResult<TChoice>> SwitchResult<TContext, TChoice>(
        TContext context,
        string question,
        IReadOnlyList<TChoice> choices,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a score within the inclusive requested range.</summary>
    Task<double> Score<TContext>(
        TContext context,
        string question,
        double minimum = 0,
        double maximum = 1,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a score with fallback and provider provenance.</summary>
    Task<SmartResult<double>> ScoreResult<TContext>(
        TContext context,
        string question,
        double minimum = 0,
        double maximum = 1,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns whether the supplied context passes the validation question.</summary>
    Task<bool> Validate<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a validation with fallback and provider provenance.</summary>
    Task<SmartResult<bool>> ValidateResult<TContext>(
        TContext context,
        string question,
        Action<SmartOptions>? configure = null,
        CancellationToken cancellationToken = default);
}
