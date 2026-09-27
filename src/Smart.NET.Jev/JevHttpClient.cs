using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Smart.NET.Abstractions;

namespace Smart.NET.Jev;

/// <summary>Sends requests to the official TypeSafe AI System One endpoint.</summary>
public sealed class JevHttpClient
{
    private const string Endpoint = "v1/systemone";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly Uri _baseUri;

    /// <summary>Creates an adapter HTTP client using the shared factory-managed client.</summary>
    /// <param name="httpClient">The factory-managed HTTP client.</param>
    /// <param name="options">The Jev provider settings.</param>
    public JevHttpClient(HttpClient httpClient, IOptions<JevOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        var jevOptions = options.Value;
        _apiKey = jevOptions.GetValidatedApiKey();
        _baseUri = jevOptions.GetValidatedBaseUri();
        Model = jevOptions.GetValidatedModel();
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= EnsureTrailingSlash(_baseUri);
    }

    internal string Model { get; }

    internal async Task<JsonDocument> DecideAsync(
        JevApiRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new SmartProviderException(
                "The Jev request timed out.",
                isTransient: true,
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SmartProviderException(
                "The Jev service could not be reached.",
                isTransient: true,
                innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpException(response);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new SmartInvalidDecisionException($"Jev returned a malformed JSON response: {exception.Message}");
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SmartProviderException(
                    "The Jev response timed out.",
                    isTransient: true,
                    innerException: exception);
            }
            catch (IOException exception)
            {
                throw new SmartProviderException(
                    "The Jev response could not be read.",
                    isTransient: true,
                    innerException: exception);
            }
        }
    }

    private static Exception CreateHttpException(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var isTransient = response.StatusCode == HttpStatusCode.RequestTimeout
            || response.StatusCode == HttpStatusCode.TooManyRequests
            || statusCode is >= 500 and <= 599;
        var retryAfter = GetRetryAfter(response);
        var exception = new JevException(statusCode, retryAfter);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new SmartConfigurationException("Jev rejected the API credentials or permissions.", exception);
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return new SmartConfigurationException("Jev rejected the decision request.", exception);
        }

        return new SmartProviderException(
            $"The Jev API returned HTTP status {statusCode}.",
            isTransient,
            exception,
            retryAfter);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
        {
            retryAfter = date - DateTimeOffset.UtcNow;
        }

        if (retryAfter is null
            && response.Headers.TryGetValues("retry-after-ms", out var millisecondValues)
            && double.TryParse(
                millisecondValues.FirstOrDefault(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var milliseconds)
            && double.IsFinite(milliseconds)
            && milliseconds is >= 0 and <= 60_000)
        {
            retryAfter = TimeSpan.FromMilliseconds(milliseconds);
        }

        if (retryAfter < TimeSpan.Zero || retryAfter > TimeSpan.FromMinutes(1))
        {
            return null;
        }

        return retryAfter;
    }

    private static Uri EnsureTrailingSlash(Uri baseUri)
    {
        var uriText = baseUri.AbsoluteUri;
        return new Uri(uriText.EndsWith("/", StringComparison.Ordinal) ? uriText : uriText + "/", UriKind.Absolute);
    }
}

internal sealed record JevApiRequest
{
    [JsonPropertyName("state")]
    public required System.Text.Json.JsonElement State { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("questions")]
    public required IReadOnlyDictionary<string, JevQuestion> Questions { get; init; }
}

internal sealed record JevQuestion
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("instructions")]
    public required string Instructions { get; init; }

    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Criteria { get; init; }
}

/// <summary>A safe, provider-specific description of a Jev HTTP failure.</summary>
public sealed class JevException : Exception
{
    /// <summary>Initializes an instance with the response status and server retry delay.</summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    /// <param name="retryAfter">The server's accepted retry delay, when available.</param>
    public JevException(int statusCode, TimeSpan? retryAfter)
        : base($"The Jev API returned HTTP status {statusCode}.")
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the HTTP response status code.</summary>
    public int StatusCode { get; }

    /// <summary>Gets the server's accepted retry delay, when available.</summary>
    public TimeSpan? RetryAfter { get; }
}
