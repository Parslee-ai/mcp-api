namespace AnyAPI.Core.OpenApi;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AnyAPI.Core.Models;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

/// <summary>
/// Parses OpenAPI specifications into ApiRegistration models.
/// </summary>
public partial class OpenApiParser : IOpenApiParser
{
    private readonly HttpClient _httpClient;
    private readonly OperationConverter _operationConverter;

    public OpenApiParser(HttpClient httpClient, OperationConverter? operationConverter = null)
    {
        _httpClient = httpClient;
        _operationConverter = operationConverter ?? new OperationConverter();
    }

    public async Task<ApiRegistration> ParseAsync(string specUrl, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(specUrl, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var registration = await ParseStreamAsync(stream, ct);
        registration.SpecUrl = specUrl;

        return registration;
    }

    public async Task<ApiRegistration> ParseAsync(Stream specStream, string baseUrl, CancellationToken ct = default)
    {
        var registration = await ParseStreamAsync(specStream, ct);

        // Override base URL if provided
        if (!string.IsNullOrEmpty(baseUrl))
        {
            registration.BaseUrl = baseUrl.TrimEnd('/');
        }

        return registration;
    }

    private async Task<ApiRegistration> ParseStreamAsync(Stream stream, CancellationToken ct)
    {
        var reader = new OpenApiStreamReader();
        var result = await reader.ReadAsync(stream, ct);

        // Only fail on critical errors that prevent parsing (missing required fields, etc.)
        // Ignore validation warnings like duplicate path signatures which don't prevent parsing
        var criticalErrors = result.OpenApiDiagnostic.Errors
            .Where(e => !IsIgnorableValidationError(e.Message))
            .ToList();

        if (criticalErrors.Count > 0 && result.OpenApiDocument?.Paths == null)
        {
            var errors = string.Join(", ", criticalErrors.Select(e => e.Message));
            throw new OpenApiParseException($"Failed to parse OpenAPI spec: {errors}");
        }

        if (result.OpenApiDocument == null)
        {
            throw new OpenApiParseException("Failed to parse OpenAPI spec: Document is null");
        }

        var specVersion = GetSpecVersionString(result.OpenApiDiagnostic.SpecificationVersion);
        return ConvertDocument(result.OpenApiDocument, specVersion);
    }

    private static string GetSpecVersionString(Microsoft.OpenApi.OpenApiSpecVersion version) => version switch
    {
        Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0 => "2.0",
        Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0 => "3.0",
        _ => "3.0" // Default to 3.0 for unknown versions
    };

    private static bool IsIgnorableValidationError(string message)
    {
        // Ignore path signature uniqueness validation errors - these don't prevent parsing
        // Some APIs (like GitHub) have duplicate paths with different parameter types
        return message.Contains("path signature") && message.Contains("MUST be unique");
    }

    private ApiRegistration ConvertDocument(OpenApiDocument doc, string specVersion)
    {
        var baseUrl = ExtractBaseUrl(doc);
        var apiId = GenerateApiId(doc.Info.Title, baseUrl);

        var registration = new ApiRegistration
        {
            Id = apiId,
            DisplayName = doc.Info.Title,
            BaseUrl = baseUrl,
            OpenApiVersion = specVersion,
            ApiVersion = doc.Info.Version,
            Description = doc.Info.Description,
            Auth = ExtractAuthConfig(doc.Components?.SecuritySchemes, apiId),
            Endpoints = [],
            CreatedAt = DateTime.UtcNow,
            LastRefreshed = DateTime.UtcNow
        };

        foreach (var (path, pathItem) in doc.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations)
            {
                var endpoint = _operationConverter.Convert(
                    path,
                    method.ToString(),
                    operation,
                    pathItem.Parameters);

                registration.Endpoints.Add(endpoint);
            }
        }

        return registration;
    }

    private static string ExtractBaseUrl(OpenApiDocument doc)
    {
        var server = doc.Servers?.FirstOrDefault();
        if (server == null)
        {
            throw new OpenApiParseException("No server URL found in OpenAPI spec");
        }

        var url = server.Url;

        // Handle server variables (e.g., {scheme}://{host})
        foreach (var (name, variable) in server.Variables ?? new Dictionary<string, OpenApiServerVariable>())
        {
            url = url.Replace($"{{{name}}}", variable.Default);
        }

        return url.TrimEnd('/');
    }

    private static AuthConfiguration ExtractAuthConfig(
        IDictionary<string, OpenApiSecurityScheme>? schemes,
        string apiId)
    {
        if (schemes == null || schemes.Count == 0)
            return new NoAuthConfig();

        // Take first security scheme as default
        var (name, scheme) = schemes.First();

        return scheme.Type switch
        {
            SecuritySchemeType.ApiKey => new ApiKeyAuthConfig
            {
                Name = name,
                In = scheme.In.ToString().ToLowerInvariant(),
                ParameterName = scheme.Name,
                Secret = new SecretReference { SecretName = $"{apiId}-apikey" }
            },
            SecuritySchemeType.Http when scheme.Scheme?.ToLowerInvariant() == "bearer" => new BearerTokenAuthConfig
            {
                Name = name,
                Secret = new SecretReference { SecretName = $"{apiId}-token" }
            },
            SecuritySchemeType.Http when scheme.Scheme?.ToLowerInvariant() == "basic" => new BasicAuthConfig
            {
                Name = name,
                Username = new SecretReference { SecretName = $"{apiId}-username" },
                Password = new SecretReference { SecretName = $"{apiId}-password" }
            },
            SecuritySchemeType.OAuth2 => ConvertOAuth2(name, scheme, apiId),
            _ => new NoAuthConfig()
        };
    }

    private static OAuth2AuthConfig ConvertOAuth2(string name, OpenApiSecurityScheme scheme, string apiId)
    {
        var flow = scheme.Flows?.ClientCredentials
                   ?? scheme.Flows?.AuthorizationCode
                   ?? throw new OpenApiParseException("No supported OAuth2 flow found");

        return new OAuth2AuthConfig
        {
            Name = name,
            Flow = scheme.Flows?.ClientCredentials != null ? "clientCredentials" : "authorizationCode",
            TokenUrl = flow.TokenUrl?.ToString() ?? throw new OpenApiParseException("Missing token URL"),
            AuthorizationUrl = flow.AuthorizationUrl?.ToString(),
            ClientId = new SecretReference { SecretName = $"{apiId}-clientid" },
            ClientSecret = new SecretReference { SecretName = $"{apiId}-clientsecret" },
            Scopes = flow.Scopes?.Keys.ToList() ?? []
        };
    }

    private static string GenerateApiId(string title, string baseUrl)
    {
        // Convert "GitHub REST API" -> "github-rest-api"
        var cleaned = AlphanumericRegex().Replace(title.ToLowerInvariant(), " ");
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var baseName = parts.Length == 0 ? "api" : string.Join("-", parts);

        // Add a short hash of the base URL to prevent collision attacks
        // This ensures two APIs with the same title but different URLs get unique IDs
        var urlHash = ComputeShortHash(baseUrl);

        return $"{baseName}-{urlHash}";
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Use first 4 bytes (8 hex chars) for a short but unique suffix
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    [GeneratedRegex("[^a-z0-9 ]")]
    private static partial Regex AlphanumericRegex();
}
