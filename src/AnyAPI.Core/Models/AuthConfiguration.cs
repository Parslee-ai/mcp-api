namespace AnyAPI.Core.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Polymorphic authentication configuration.
/// Type discriminator determines concrete type during JSON serialization.
/// </summary>
[JsonConverter(typeof(AuthConfigurationConverter))]
public abstract class AuthConfiguration
{
    /// <summary>Type discriminator for serialization.</summary>
    [JsonPropertyName("authType")]
    public abstract string AuthType { get; }

    /// <summary>Human-readable name for this auth configuration.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Custom JSON converter for AuthConfiguration that handles missing discriminators.
/// </summary>
public class AuthConfigurationConverter : JsonConverter<AuthConfiguration>
{
    public override AuthConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var authType = root.TryGetProperty("authType", out var typeElement)
            ? typeElement.GetString()
            : "none"; // Default to NoAuth if no discriminator

        AuthConfiguration result = authType switch
        {
            "apiKey" => JsonSerializer.Deserialize<ApiKeyAuthConfig>(root.GetRawText(), GetOptionsWithoutConverter(options))!,
            "bearer" => JsonSerializer.Deserialize<BearerTokenAuthConfig>(root.GetRawText(), GetOptionsWithoutConverter(options))!,
            "basic" => JsonSerializer.Deserialize<BasicAuthConfig>(root.GetRawText(), GetOptionsWithoutConverter(options))!,
            "oauth2" => JsonSerializer.Deserialize<OAuth2AuthConfig>(root.GetRawText(), GetOptionsWithoutConverter(options))!,
            _ => JsonSerializer.Deserialize<NoAuthConfig>(root.GetRawText(), GetOptionsWithoutConverter(options))!
        };

        return result;
    }

    public override void Write(Utf8JsonWriter writer, AuthConfiguration value, JsonSerializerOptions options)
    {
        var opts = GetOptionsWithoutConverter(options);
        JsonSerializer.Serialize(writer, value, value.GetType(), opts);
    }

    private static JsonSerializerOptions GetOptionsWithoutConverter(JsonSerializerOptions options)
    {
        var newOptions = new JsonSerializerOptions(options);
        // Remove this converter to avoid infinite recursion
        for (int i = newOptions.Converters.Count - 1; i >= 0; i--)
        {
            if (newOptions.Converters[i] is AuthConfigurationConverter)
            {
                newOptions.Converters.RemoveAt(i);
            }
        }
        return newOptions;
    }
}

/// <summary>No authentication required.</summary>
public class NoAuthConfig : AuthConfiguration
{
    public override string AuthType => "none";
}

/// <summary>API key authentication (header, query, or cookie).</summary>
public class ApiKeyAuthConfig : AuthConfiguration
{
    public override string AuthType => "apiKey";

    /// <summary>Where to place the key: header, query, cookie.</summary>
    public required string In { get; set; }

    /// <summary>Parameter/header name (e.g., "X-API-Key", "api_key").</summary>
    public required string ParameterName { get; set; }

    /// <summary>Reference to secret in Key Vault.</summary>
    public required SecretReference Secret { get; set; }
}

/// <summary>Bearer token authentication.</summary>
public class BearerTokenAuthConfig : AuthConfiguration
{
    public override string AuthType => "bearer";

    /// <summary>Reference to token in Key Vault.</summary>
    public required SecretReference Secret { get; set; }

    /// <summary>Authorization header prefix (default "Bearer").</summary>
    public string Prefix { get; set; } = "Bearer";
}

/// <summary>HTTP Basic authentication.</summary>
public class BasicAuthConfig : AuthConfiguration
{
    public override string AuthType => "basic";

    /// <summary>Username secret reference.</summary>
    public required SecretReference Username { get; set; }

    /// <summary>Password secret reference.</summary>
    public required SecretReference Password { get; set; }
}

/// <summary>OAuth2 authentication with automatic token refresh.</summary>
public class OAuth2AuthConfig : AuthConfiguration
{
    public override string AuthType => "oauth2";

    /// <summary>OAuth2 flow type: "clientCredentials" or "authorizationCode".</summary>
    public required string Flow { get; set; }

    /// <summary>Token endpoint URL.</summary>
    public required string TokenUrl { get; set; }

    /// <summary>Authorization endpoint (for authorization code flow).</summary>
    public string? AuthorizationUrl { get; set; }

    /// <summary>Client ID secret reference.</summary>
    public required SecretReference ClientId { get; set; }

    /// <summary>Client secret reference.</summary>
    public required SecretReference ClientSecret { get; set; }

    /// <summary>Required OAuth scopes.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>Refresh token reference (for authorization code flow).</summary>
    public SecretReference? RefreshToken { get; set; }
}
