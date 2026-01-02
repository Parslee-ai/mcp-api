namespace McpApi.Mcp;

using System.ComponentModel;
using System.Text.Json;
using McpApi.Core.Http;
using McpApi.Core.Models;
using McpApi.Core.Services;
using McpApi.Core.Storage;
using ModelContextProtocol.Server;

/// <summary>
/// Provides MCP tools dynamically from registered API specifications.
/// All operations are scoped to the authenticated user with usage tracking.
/// </summary>
public class DynamicToolProvider
{
    private readonly IApiRegistrationStore _store;
    private readonly IApiClient _apiClient;
    private readonly IMcpCurrentUser _currentUser;
    private readonly IUsageTrackingService _usageTracking;

    public DynamicToolProvider(
        IApiRegistrationStore store,
        IApiClient apiClient,
        IMcpCurrentUser currentUser,
        IUsageTrackingService usageTracking)
    {
        _store = store;
        _apiClient = apiClient;
        _currentUser = currentUser;
        _usageTracking = usageTracking;
    }

    private string UserId => _currentUser.UserId;
    private string UserTier => _currentUser.Tier;

    [McpServerTool, Description("List all available API tools")]
    public async Task<string> ListAvailableApis(CancellationToken ct = default)
    {
        var apis = await _store.GetEnabledAsync(UserId, ct);
        var result = apis.Select(a => new
        {
            a.Id,
            a.DisplayName,
            EndpointCount = a.EnabledEndpointCount,
            a.BaseUrl
        });
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Execute a dynamic API call")]
    public async Task<string> CallApi(
        [Description("API ID (e.g., 'github')")] string apiId,
        [Description("Endpoint operation ID")] string operationId,
        [Description("Parameters as JSON object")] string? parametersJson = null,
        CancellationToken ct = default)
    {
        // Check usage limits before making the call
        try
        {
            await _usageTracking.CheckAndRecordApiCallAsync(UserId, UserTier, ct);
        }
        catch (UsageLimitExceededException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Usage limit exceeded",
                limitType = ex.LimitType,
                currentUsage = ex.CurrentUsage,
                limit = ex.Limit,
                message = $"You have reached your {ex.LimitType} limit ({ex.Limit}). Upgrade your plan to continue."
            });
        }

        var api = await _store.GetAsync(UserId, apiId, ct);
        if (api == null)
            return JsonSerializer.Serialize(new { error = $"API '{apiId}' not found" });

        // Load endpoints separately (they're stored in a different container)
        var endpoints = await _store.GetEndpointsAsync(UserId, apiId, ct);
        var endpoint = endpoints.FirstOrDefault(e =>
            e.OperationId.Equals(operationId, StringComparison.OrdinalIgnoreCase) ||
            e.Id.Equals(operationId, StringComparison.OrdinalIgnoreCase));

        if (endpoint == null)
            return JsonSerializer.Serialize(new { error = $"Endpoint '{operationId}' not found in API '{apiId}'" });

        if (!endpoint.IsEnabled)
            return JsonSerializer.Serialize(new { error = $"Endpoint '{operationId}' is disabled" });

        var parameters = string.IsNullOrEmpty(parametersJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(parametersJson) ?? new();

        // Convert JsonElement values to proper types
        var convertedParams = new Dictionary<string, object?>();
        foreach (var (key, value) in parameters)
        {
            convertedParams[key] = value switch
            {
                JsonElement je => ConvertJsonElement(je),
                _ => value
            };
        }

        var response = await _apiClient.ExecuteAsync(api, endpoint, convertedParams, _currentUser.SecretContext, ct);

        return JsonSerializer.Serialize(new
        {
            statusCode = response.StatusCode,
            success = response.IsSuccess,
            body = TryParseJson(response.Body),
            headers = response.Headers
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Get details about a specific API and its endpoints")]
    public async Task<string> GetApiDetails(
        [Description("API ID (e.g., 'github')")] string apiId,
        CancellationToken ct = default)
    {
        var api = await _store.GetAsync(UserId, apiId, ct);
        if (api == null)
            return JsonSerializer.Serialize(new { error = $"API '{apiId}' not found" });

        // Load endpoints separately (they're stored in a different container)
        var endpoints = await _store.GetEndpointsAsync(UserId, apiId, ct);

        var result = new
        {
            api.Id,
            api.DisplayName,
            api.Description,
            api.BaseUrl,
            AuthType = api.Auth.GetType().Name.Replace("Config", ""),
            api.IsEnabled,
            Endpoints = endpoints
                .Where(e => e.IsEnabled)
                .Select(e => new
                {
                    e.OperationId,
                    e.Method,
                    e.Path,
                    e.Summary,
                    e.Tags,
                    Parameters = e.Parameters.Select(p => new
                    {
                        p.Name,
                        p.In,
                        p.Required,
                        p.Description,
                        Type = p.Schema.Type
                    })
                })
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Search for endpoints across all APIs")]
    public async Task<string> SearchEndpoints(
        [Description("Search query (searches operation IDs, paths, and summaries)")] string query,
        CancellationToken ct = default)
    {
        // Single query across all endpoints instead of N+1
        var results = await _store.SearchEndpointsAsync(UserId, query, limit: 20, ct);

        return JsonSerializer.Serialize(new { count = results.Count, results },
            new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Get your current usage and remaining quota")]
    public async Task<string> GetUsage(CancellationToken ct = default)
    {
        var summary = await _usageTracking.GetUsageSummaryAsync(UserId, UserTier, ct);

        return JsonSerializer.Serialize(new
        {
            tier = summary.Tier,
            month = summary.YearMonth,
            apiCalls = new
            {
                used = summary.ApiCallsUsed,
                limit = summary.ApiCallsLimit == int.MaxValue ? "unlimited" : summary.ApiCallsLimit.ToString(),
                remaining = summary.ApiCallsRemaining == int.MaxValue ? "unlimited" : summary.ApiCallsRemaining.ToString()
            },
            apis = new
            {
                registered = summary.ApisRegistered,
                limit = summary.ApisLimit == int.MaxValue ? "unlimited" : summary.ApisLimit.ToString(),
                remaining = summary.ApisRemaining == int.MaxValue ? "unlimited" : summary.ApisRemaining.ToString()
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText()
        };
    }

    private static object? TryParseJson(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch
        {
            return body;
        }
    }
}
