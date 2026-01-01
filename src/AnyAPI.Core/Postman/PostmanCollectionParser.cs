namespace AnyAPI.Core.Postman;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnyAPI.Core.Models;

/// <summary>
/// Parses Postman Collection v2.1 format into ApiRegistration.
/// </summary>
public class PostmanCollectionParser
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PostmanCollectionParser(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Checks if the given JSON content is a Postman Collection.
    /// </summary>
    public static bool IsPostmanCollection(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Postman collections have an "info" object with a "schema" containing "postman"
            if (root.TryGetProperty("info", out var info))
            {
                if (info.TryGetProperty("schema", out var schema))
                {
                    var schemaStr = schema.GetString();
                    return schemaStr?.Contains("postman", StringComparison.OrdinalIgnoreCase) == true;
                }
                // Also check for _postman_id
                if (info.TryGetProperty("_postman_id", out _))
                {
                    return true;
                }
            }

            // Check for item array with request objects (Postman structure)
            if (root.TryGetProperty("item", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("request", out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a Postman Collection from a URL.
    /// </summary>
    public async Task<ApiRegistration> ParseAsync(string url, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseFromJson(json, url);
    }

    /// <summary>
    /// Parses a Postman Collection from JSON content.
    /// </summary>
    public ApiRegistration ParseFromJson(string json, string? sourceUrl = null)
    {
        var collection = JsonSerializer.Deserialize<PostmanCollection>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse Postman collection");

        var baseUrl = ExtractBaseUrl(collection);
        var id = GenerateId(collection.Info.Name, sourceUrl ?? baseUrl);

        var registration = new ApiRegistration
        {
            Id = id,
            DisplayName = collection.Info.Name,
            Description = collection.Info.Description,
            BaseUrl = baseUrl,
            SpecUrl = sourceUrl,
            OpenApiVersion = "postman-2.1",
            Auth = ConvertAuth(collection.Auth),
            CreatedAt = DateTime.UtcNow,
            LastRefreshed = DateTime.UtcNow
        };

        // Flatten all items into endpoints
        var endpoints = new List<ApiEndpoint>();
        FlattenItems(collection.Item, endpoints, [], collection.Variable);
        registration.Endpoints = endpoints;

        return registration;
    }

    private static string ExtractBaseUrl(PostmanCollection collection)
    {
        // Try to get base URL from collection variables
        var baseUrlVar = collection.Variable?.FirstOrDefault(v =>
            v.Key.Equals("baseUrl", StringComparison.OrdinalIgnoreCase) ||
            v.Key.Equals("base_url", StringComparison.OrdinalIgnoreCase) ||
            v.Key.Equals("host", StringComparison.OrdinalIgnoreCase));

        if (baseUrlVar?.Value != null)
        {
            return baseUrlVar.Value;
        }

        // Try to extract from first request URL
        var firstRequest = FindFirstRequest(collection.Item);
        if (firstRequest?.Url != null)
        {
            var url = firstRequest.Url;
            if (url.Host != null && url.Host.Count > 0)
            {
                var protocol = url.Protocol ?? "https";
                var host = string.Join(".", url.Host);
                return $"{protocol}://{host}";
            }

            // Parse from raw URL
            if (!string.IsNullOrEmpty(url.Raw))
            {
                var rawUrl = SubstituteVariables(url.Raw, collection.Variable);
                if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
                {
                    return $"{uri.Scheme}://{uri.Host}";
                }
            }
        }

        return "https://api.example.com";
    }

    private static PostmanRequest? FindFirstRequest(List<PostmanItem>? items)
    {
        if (items == null) return null;

        foreach (var item in items)
        {
            if (item.Request != null)
                return item.Request;

            var nested = FindFirstRequest(item.Item);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void FlattenItems(
        List<PostmanItem>? items,
        List<ApiEndpoint> endpoints,
        List<string> folderPath,
        List<PostmanVariable>? variables)
    {
        if (items == null) return;

        foreach (var item in items)
        {
            if (item.Request != null)
            {
                // This is an endpoint
                var endpoint = ConvertToEndpoint(item, folderPath, variables);
                if (endpoint != null)
                {
                    endpoints.Add(endpoint);
                }
            }
            else if (item.Item != null)
            {
                // This is a folder - recurse
                var newPath = new List<string>(folderPath) { item.Name };
                FlattenItems(item.Item, endpoints, newPath, variables);
            }
        }
    }

    private ApiEndpoint? ConvertToEndpoint(
        PostmanItem item,
        List<string> folderPath,
        List<PostmanVariable>? variables)
    {
        var request = item.Request;
        if (request?.Url == null) return null;

        var path = BuildPath(request.Url, variables);
        if (string.IsNullOrEmpty(path)) return null;

        var operationId = GenerateOperationId(item.Name, request.Method);
        var tag = folderPath.Count > 0 ? folderPath[0] : "default";

        var endpoint = new ApiEndpoint
        {
            Id = operationId,
            OperationId = operationId,
            Method = request.Method.ToUpperInvariant(),
            Path = path,
            Summary = item.Name,
            Description = request.Description ?? item.Description,
            Tags = folderPath.Count > 0 ? [tag] : ["default"],
            Parameters = [],
            Responses = new Dictionary<string, ResponseDefinition>
            {
                ["200"] = new ResponseDefinition { Description = "Successful response" }
            }
        };

        // Extract path parameters (variables in the path like :id or {{id}})
        var pathParams = ExtractPathParameters(path, request.Url.Variable);
        endpoint.Parameters.AddRange(pathParams);

        // Extract query parameters
        if (request.Url.Query != null)
        {
            foreach (var query in request.Url.Query.Where(q => q.Disabled != true))
            {
                endpoint.Parameters.Add(new ParameterDefinition
                {
                    Name = query.Key,
                    In = "query",
                    Required = false,
                    Description = query.Description,
                    Schema = new JsonSchema { Type = "string" }
                });
            }
        }

        // Extract header parameters (excluding common headers)
        if (request.Header != null)
        {
            var excludedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Content-Type", "Accept", "Authorization", "User-Agent"
            };

            foreach (var header in request.Header.Where(h =>
                h.Disabled != true && !excludedHeaders.Contains(h.Key)))
            {
                endpoint.Parameters.Add(new ParameterDefinition
                {
                    Name = header.Key,
                    In = "header",
                    Required = false,
                    Description = header.Description,
                    Schema = new JsonSchema { Type = "string" }
                });
            }
        }

        // Extract request body
        if (request.Body != null && !string.IsNullOrEmpty(request.Body.Raw))
        {
            endpoint.RequestBody = new RequestBodyDefinition
            {
                Required = true,
                Description = "Request body",
                Content = new Dictionary<string, JsonSchema>
                {
                    ["application/json"] = InferSchemaFromExample(request.Body.Raw)
                }
            };
        }
        else if (request.Body?.FormData != null || request.Body?.UrlEncoded != null)
        {
            var formFields = request.Body.FormData ?? request.Body.UrlEncoded ?? [];
            var contentType = request.Body.UrlEncoded != null
                ? "application/x-www-form-urlencoded"
                : "multipart/form-data";

            var properties = new Dictionary<string, JsonSchema>();
            var required = new List<string>();

            foreach (var field in formFields.Where(f => f.Disabled != true))
            {
                properties[field.Key] = new JsonSchema
                {
                    Type = field.Type == "file" ? "string" : "string",
                    Format = field.Type == "file" ? "binary" : null,
                    Description = field.Description
                };
            }

            endpoint.RequestBody = new RequestBodyDefinition
            {
                Required = true,
                Content = new Dictionary<string, JsonSchema>
                {
                    [contentType] = new JsonSchema
                    {
                        Type = "object",
                        Properties = properties
                    }
                }
            };
        }

        return endpoint;
    }

    private static string BuildPath(PostmanUrl url, List<PostmanVariable>? variables)
    {
        if (url.Path != null && url.Path.Count > 0)
        {
            // Convert path segments, replacing :param with {param}
            var segments = url.Path.Select(segment =>
            {
                if (segment.StartsWith(':'))
                {
                    return "{" + segment[1..] + "}";
                }
                // Handle {{variable}} syntax
                if (segment.StartsWith("{{") && segment.EndsWith("}}"))
                {
                    var varName = segment[2..^2];
                    // Check if it's a path parameter or a variable
                    var variable = variables?.FirstOrDefault(v => v.Key == varName);
                    if (variable?.Value != null && !variable.Value.Contains("{{"))
                    {
                        return variable.Value;
                    }
                    return "{" + varName + "}";
                }
                return segment;
            });

            return "/" + string.Join("/", segments);
        }

        // Parse from raw URL
        if (!string.IsNullOrEmpty(url.Raw))
        {
            var raw = url.Raw;
            // Remove protocol and host
            if (Uri.TryCreate(SubstituteVariables(raw, variables), UriKind.Absolute, out var uri))
            {
                var path = uri.AbsolutePath;
                // Convert :param to {param}
                path = Regex.Replace(path, @":(\w+)", "{$1}");
                return path;
            }
        }

        return "/";
    }

    private static List<ParameterDefinition> ExtractPathParameters(
        string path,
        List<PostmanVariable>? urlVariables)
    {
        var parameters = new List<ParameterDefinition>();

        // Find {param} in path
        var matches = Regex.Matches(path, @"\{(\w+)\}");
        foreach (Match match in matches)
        {
            var paramName = match.Groups[1].Value;
            var variable = urlVariables?.FirstOrDefault(v =>
                v.Key.Equals(paramName, StringComparison.OrdinalIgnoreCase));

            parameters.Add(new ParameterDefinition
            {
                Name = paramName,
                In = "path",
                Required = true,
                Description = variable?.Description,
                Schema = new JsonSchema { Type = variable?.Type ?? "string" }
            });
        }

        return parameters;
    }

    private static JsonSchema InferSchemaFromExample(string jsonExample)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonExample);
            return InferSchemaFromElement(doc.RootElement);
        }
        catch
        {
            return new JsonSchema { Type = "object" };
        }
    }

    private static JsonSchema InferSchemaFromElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => new JsonSchema
            {
                Type = "object",
                Properties = element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => InferSchemaFromElement(p.Value))
            },
            JsonValueKind.Array => new JsonSchema
            {
                Type = "array",
                Items = element.GetArrayLength() > 0
                    ? InferSchemaFromElement(element[0])
                    : new JsonSchema { Type = "object" }
            },
            JsonValueKind.String => new JsonSchema { Type = "string" },
            JsonValueKind.Number => element.TryGetInt64(out _)
                ? new JsonSchema { Type = "integer" }
                : new JsonSchema { Type = "number" },
            JsonValueKind.True or JsonValueKind.False => new JsonSchema { Type = "boolean" },
            _ => new JsonSchema { Type = "string" }
        };
    }

    private static AuthConfiguration ConvertAuth(PostmanAuth? auth)
    {
        if (auth == null) return new NoAuthConfig();

        return auth.Type.ToLowerInvariant() switch
        {
            "bearer" => new BearerTokenAuthConfig
            {
                Name = "bearer",
                Secret = new SecretReference { SecretName = "bearer-token" }
            },
            "apikey" => new ApiKeyAuthConfig
            {
                Name = "apikey",
                In = GetAuthParamValue(auth.ApiKey, "in") ?? "header",
                ParameterName = GetAuthParamValue(auth.ApiKey, "key") ?? "X-API-Key",
                Secret = new SecretReference { SecretName = "api-key" }
            },
            "basic" => new BasicAuthConfig
            {
                Name = "basic",
                Username = new SecretReference { SecretName = "username" },
                Password = new SecretReference { SecretName = "password" }
            },
            "oauth2" => new OAuth2AuthConfig
            {
                Name = "oauth2",
                Flow = "clientCredentials",
                TokenUrl = GetAuthParamValue(auth.OAuth2, "accessTokenUrl") ?? "",
                ClientId = new SecretReference { SecretName = "client-id" },
                ClientSecret = new SecretReference { SecretName = "client-secret" }
            },
            _ => new NoAuthConfig()
        };
    }

    private static string? GetAuthParamValue(List<PostmanAuthParam>? params_, string key)
    {
        return params_?.FirstOrDefault(p =>
            p.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static string GenerateId(string name, string sourceUrl)
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        var baseName = string.IsNullOrEmpty(slug) ? "api" : slug;

        // Add hash of source URL to prevent collision attacks
        var urlHash = ComputeShortHash(sourceUrl);
        return $"{baseName}-{urlHash}";
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    private static string GenerateOperationId(string name, string method)
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return $"{method.ToLowerInvariant()}-{slug}";
    }

    private static string SubstituteVariables(string input, List<PostmanVariable>? variables)
    {
        if (variables == null || string.IsNullOrEmpty(input))
            return input;

        var result = input;
        foreach (var variable in variables)
        {
            if (!string.IsNullOrEmpty(variable.Value))
            {
                result = result.Replace($"{{{{{variable.Key}}}}}", variable.Value);
            }
        }
        return result;
    }
}
