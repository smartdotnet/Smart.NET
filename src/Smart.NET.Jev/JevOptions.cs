using Smart.NET.Abstractions;

namespace Smart.NET.Jev;

/// <summary>Configures the TypeSafe AI Jev provider.</summary>
public sealed class JevOptions
{
    /// <summary>Gets or sets the API key; defaults to <c>TYPESAFE_API_KEY</c>.</summary>
    public string? ApiKey { get; set; } = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");

    /// <summary>Gets or sets the API root; defaults to the official TypeSafe API.</summary>
    public string BaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("TYPESAFE_BASE_URL") ?? "https://api.typesafe.ai";

    /// <summary>Gets or sets the model; defaults to <c>jev-latest</c>.</summary>
    public string Model { get; set; } =
        Environment.GetEnvironmentVariable("TYPESAFE_DEFAULT_MODEL") ?? "jev-latest";

    internal string GetValidatedApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new SmartConfigurationException(
                "A Jev API key is required. Set JevOptions.ApiKey or the TYPESAFE_API_KEY environment variable.");
        }

        var apiKey = ApiKey.Trim();
        if (apiKey.Any(character => char.IsWhiteSpace(character)
            || char.IsControl(character)
            || character > 0x7f))
        {
            throw new SmartConfigurationException("The Jev API key contains invalid characters.");
        }

        return apiKey;
    }

    internal Uri GetValidatedBaseUri()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps
                && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment)
            || !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new SmartConfigurationException(
                "The Jev API base URL must be an absolute HTTPS URL (loopback HTTP is allowed for local testing).");
        }

        return baseUri;
    }

    internal string GetValidatedModel()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new SmartConfigurationException("The Jev model name cannot be empty.");
        }

        return Model.Trim();
    }
}
