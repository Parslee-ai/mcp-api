namespace AnyAPI.Core.Http;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using AnyAPI.Core.Models;

/// <summary>
/// Builds HTTP requests from endpoint definitions and parameters.
/// </summary>
public static partial class RequestBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Builds an HttpRequestMessage from endpoint definition and parameters.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when required parameters are missing.</exception>
    public static HttpRequestMessage Build(
        string baseUrl,
        ApiEndpoint endpoint,
        Dictionary<string, object?> parameters)
    {
        // Validate required parameters
        ValidateRequiredParameters(endpoint, parameters);

        // Substitute path parameters
        var path = SubstitutePathParameters(endpoint.Path, parameters, endpoint.Parameters);

        // Build query string
        var queryParams = BuildQueryParameters(parameters, endpoint.Parameters);
        var uri = BuildUri(baseUrl, path, queryParams);

        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), uri);

        // Add header parameters
        AddHeaderParameters(request, parameters, endpoint.Parameters);

        // Add request body for POST/PUT/PATCH
        if (endpoint.RequestBody != null && HasBodyMethod(endpoint.Method))
        {
            var bodyContent = BuildRequestBody(parameters, endpoint);
            if (bodyContent != null)
            {
                request.Content = bodyContent;
            }
        }

        return request;
    }

    private static string SubstitutePathParameters(
        string path,
        Dictionary<string, object?> parameters,
        List<ParameterDefinition> parameterDefs)
    {
        var result = path;
        var pathParams = parameterDefs.Where(p => p.In == "path");

        foreach (var param in pathParams)
        {
            if (parameters.TryGetValue(param.Name, out var value) && value != null)
            {
                result = result.Replace($"{{{param.Name}}}", Uri.EscapeDataString(value.ToString()!));
            }
        }

        return result;
    }

    private static Dictionary<string, string> BuildQueryParameters(
        Dictionary<string, object?> parameters,
        List<ParameterDefinition> parameterDefs)
    {
        var queryParams = new Dictionary<string, string>();
        var queryDefs = parameterDefs.Where(p => p.In == "query");

        foreach (var param in queryDefs)
        {
            if (parameters.TryGetValue(param.Name, out var value) && value != null)
            {
                queryParams[param.Name] = value.ToString()!;
            }
        }

        return queryParams;
    }

    private static Uri BuildUri(string baseUrl, string path, Dictionary<string, string> queryParams)
    {
        var uriBuilder = new UriBuilder(baseUrl.TrimEnd('/') + path);

        if (queryParams.Count > 0)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var (key, value) in queryParams)
            {
                query[key] = value;
            }
            uriBuilder.Query = query.ToString();
        }

        return uriBuilder.Uri;
    }

    private static void AddHeaderParameters(
        HttpRequestMessage request,
        Dictionary<string, object?> parameters,
        List<ParameterDefinition> parameterDefs)
    {
        var headerDefs = parameterDefs.Where(p => p.In == "header");

        foreach (var param in headerDefs)
        {
            if (parameters.TryGetValue(param.Name, out var value) && value != null)
            {
                request.Headers.TryAddWithoutValidation(param.Name, value.ToString());
            }
        }
    }

    private static HttpContent? BuildRequestBody(
        Dictionary<string, object?> parameters,
        ApiEndpoint endpoint)
    {
        // Check for explicit "body" parameter
        if (parameters.TryGetValue("body", out var bodyValue) && bodyValue != null)
        {
            var json = bodyValue is string s ? s : JsonSerializer.Serialize(bodyValue, JsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Build body from non-path/query/header parameters
        var bodyParams = new Dictionary<string, object?>();
        var definedParams = endpoint.Parameters
            .Where(p => p.In is "path" or "query" or "header")
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var (key, value) in parameters)
        {
            if (!definedParams.Contains(key) && key != "body" && value != null)
            {
                bodyParams[key] = value;
            }
        }

        if (bodyParams.Count > 0)
        {
            var json = JsonSerializer.Serialize(bodyParams, JsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        return null;
    }

    private static bool HasBodyMethod(string method)
    {
        return method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";
    }

    private static void ValidateRequiredParameters(
        ApiEndpoint endpoint,
        Dictionary<string, object?> parameters)
    {
        var missingParams = endpoint.Parameters
            .Where(p => p.Required)
            .Where(p => !parameters.TryGetValue(p.Name, out var value) || value == null)
            .Select(p => $"{p.Name} ({p.In})")
            .ToList();

        if (missingParams.Count > 0)
        {
            throw new ArgumentException(
                $"Missing required parameters for {endpoint.Method} {endpoint.Path}: {string.Join(", ", missingParams)}");
        }

        // Validate required request body
        if (endpoint.RequestBody?.Required == true && HasBodyMethod(endpoint.Method))
        {
            var hasBody = parameters.TryGetValue("body", out var body) && body != null;
            var hasBodyParams = parameters.Any(p =>
                !endpoint.Parameters.Any(pd => pd.Name == p.Key) &&
                p.Key != "body" &&
                p.Value != null);

            if (!hasBody && !hasBodyParams)
            {
                throw new ArgumentException(
                    $"Missing required request body for {endpoint.Method} {endpoint.Path}");
            }
        }
    }
}
