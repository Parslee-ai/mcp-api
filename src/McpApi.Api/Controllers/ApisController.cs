using System.Security.Claims;
using McpApi.Api.DTOs;
using McpApi.Api.Services;
using McpApi.Core.GraphQL;
using McpApi.Core.Models;
using McpApi.Core.OpenApi;
using McpApi.Core.Postman;
using McpApi.Core.Services;
using McpApi.Core.Storage;
using McpApi.Core.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpApi.Api.Controllers;

[ApiController]
[Route("api/apis")]
[Authorize]
public class ApisController : ControllerBase
{
    private readonly IApiRegistrationStore _store;
    private readonly IOpenApiParser _parser;
    private readonly OpenApiDiscovery _discovery;
    private readonly PostmanCollectionParser _postmanParser;
    private readonly GraphQLSchemaParser _graphqlParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly IUsageTrackingService _usageTracking;

    public ApisController(
        IApiRegistrationStore store,
        IOpenApiParser parser,
        OpenApiDiscovery discovery,
        PostmanCollectionParser postmanParser,
        GraphQLSchemaParser graphqlParser,
        IHttpClientFactory httpClientFactory,
        ICurrentUserService currentUser,
        IUsageTrackingService usageTracking)
    {
        _store = store;
        _parser = parser;
        _discovery = discovery;
        _postmanParser = postmanParser;
        _graphqlParser = graphqlParser;
        _httpClientFactory = httpClientFactory;
        _currentUser = currentUser;
        _usageTracking = usageTracking;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var apis = await _store.GetAllAsync(userId, ct);

        var results = new List<ApiRegistrationDto>();
        foreach (var api in apis)
        {
            var endpointCount = await _store.GetEndpointCountAsync(userId, api.Id, ct);
            var enabledCount = await _store.GetEnabledEndpointCountAsync(userId, api.Id, ct);
            results.Add(api.ToDto(endpointCount, enabledCount));
        }

        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);

        if (api == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        var endpoints = await _store.GetEndpointsAsync(userId, id, ct);
        return Ok(api.ToDetailDto(endpoints));
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterApiRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var userTier = await GetUserTierAsync(ct);

        try
        {
            UrlValidator.ValidateExternalUrl(request.BaseUrl);
            if (!string.IsNullOrEmpty(request.SpecUrl))
            {
                UrlValidator.ValidateExternalUrl(request.SpecUrl);
            }

            var targetUrl = request.SpecUrl ?? request.BaseUrl;

            ApiRegistration registration;

            // Check if this is a GraphQL endpoint
            if (GraphQLSchemaParser.LooksLikeGraphQLEndpoint(targetUrl))
            {
                registration = await _graphqlParser.ParseFromEndpointAsync(targetUrl, ct);
            }
            else
            {
                // Discover spec URL if not provided
                var specUrl = request.SpecUrl;
                if (string.IsNullOrEmpty(specUrl))
                {
                    specUrl = await _discovery.DiscoverAsync(request.BaseUrl, ct);
                    if (specUrl == null)
                    {
                        return BadRequest(new ErrorResponse(
                            $"Could not discover OpenAPI spec at {request.BaseUrl}. Please provide the spec URL manually."));
                    }
                }

                // Fetch and detect format
                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(specUrl, ct);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(ct);

                if (PostmanCollectionParser.IsPostmanCollection(content))
                {
                    registration = _postmanParser.ParseFromJson(content, specUrl);
                }
                else if (GraphQLSchemaParser.IsGraphQLSchema(content))
                {
                    registration = _graphqlParser.ParseFromSdl(content, request.BaseUrl);
                }
                else
                {
                    registration = await _parser.ParseAsync(specUrl, ct);
                }
            }

            // Check if API already exists
            if (await _store.ExistsAsync(userId, registration.Id, ct))
            {
                return Conflict(new ErrorResponse($"API '{registration.Id}' is already registered"));
            }

            var endpoints = registration.Endpoints.ToList();

            // Check usage limits
            var currentApiCount = await _store.GetApiCountAsync(userId, ct);
            if (!_usageTracking.CanRegisterApi(userTier, currentApiCount))
            {
                var (_, maxApis, _) = TierLimits.GetLimits(userTier);
                return BadRequest(new ErrorResponse($"API limit reached. Your {userTier} tier allows {maxApis} APIs."));
            }

            var (_, _, maxEndpoints) = TierLimits.GetLimits(userTier);
            if (maxEndpoints != int.MaxValue && endpoints.Count > maxEndpoints)
            {
                return BadRequest(new ErrorResponse(
                    $"This API has {endpoints.Count} endpoints, which exceeds your tier limit of {maxEndpoints}."));
            }

            // Set user ownership and save
            registration.UserId = userId;
            await _store.UpsertAsync(registration, ct);
            await _store.SaveEndpointsAsync(userId, registration.Id, endpoints, ct);

            return CreatedAtAction(nameof(Get), new { id = registration.Id },
                registration.ToDto(endpoints.Count, endpoints.Count(e => e.IsEnabled)));
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateApiRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);

        if (api == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        if (request.DisplayName != null)
        {
            api.DisplayName = request.DisplayName;
        }

        if (request.IsEnabled.HasValue)
        {
            api.IsEnabled = request.IsEnabled.Value;
        }

        await _store.UpsertAsync(api, ct);

        var endpointCount = await _store.GetEndpointCountAsync(userId, id, ct);
        var enabledCount = await _store.GetEnabledEndpointCountAsync(userId, id, ct);
        return Ok(api.ToDto(endpointCount, enabledCount));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);

        if (api == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        await _store.DeleteAsync(userId, id, ct);
        return NoContent();
    }

    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> Toggle(string id, [FromBody] ToggleRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);

        if (api == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        api.IsEnabled = request.Enabled;
        await _store.UpsertAsync(api, ct);

        var endpointCount = await _store.GetEndpointCountAsync(userId, id, ct);
        var enabledCount = await _store.GetEnabledEndpointCountAsync(userId, id, ct);
        return Ok(api.ToDto(endpointCount, enabledCount));
    }

    [HttpPost("{id}/refresh")]
    public async Task<IActionResult> Refresh(string id, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var existingApi = await _store.GetAsync(userId, id, ct);

        if (existingApi == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        if (string.IsNullOrEmpty(existingApi.SpecUrl))
        {
            return BadRequest(new ErrorResponse($"API '{id}' does not have a spec URL to refresh from"));
        }

        try
        {
            var existingEndpoints = await _store.GetEndpointsAsync(userId, id, ct);
            var refreshedApi = await _parser.ParseAsync(existingApi.SpecUrl, ct);
            var newEndpoints = refreshedApi.Endpoints.ToList();

            // Preserve settings
            refreshedApi.Id = existingApi.Id;
            refreshedApi.UserId = userId;
            refreshedApi.IsEnabled = existingApi.IsEnabled;
            refreshedApi.Auth = existingApi.Auth;
            refreshedApi.CreatedAt = existingApi.CreatedAt;
            refreshedApi.ETag = existingApi.ETag;

            // Preserve endpoint enabled states
            foreach (var endpoint in newEndpoints)
            {
                var existing = existingEndpoints.FirstOrDefault(e => e.OperationId == endpoint.OperationId);
                if (existing != null)
                {
                    endpoint.IsEnabled = existing.IsEnabled;
                }
            }

            await _store.UpsertAsync(refreshedApi, ct);
            await _store.SaveEndpointsAsync(userId, id, newEndpoints, ct);

            return Ok(refreshedApi.ToDetailDto(newEndpoints));
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponse($"Failed to refresh API: {ex.Message}"));
        }
    }

    [HttpPut("{id}/auth")]
    public async Task<IActionResult> UpdateAuth(string id, [FromBody] UpdateAuthConfigRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);

        if (api == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        // TODO: Implement auth config conversion and secret encryption
        // For now, just return not implemented
        return StatusCode(501, new ErrorResponse("Auth configuration update not yet implemented"));
    }

    [HttpGet("{id}/endpoints")]
    public async Task<IActionResult> GetEndpoints(string id, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var api = await _store.GetAsync(userId, id, ct);

        if (api == null)
        {
            return NotFound(new ErrorResponse($"API '{id}' not found"));
        }

        var endpoints = await _store.GetEndpointsAsync(userId, id, ct);
        return Ok(endpoints.Select(e => e.ToDto()));
    }

    [HttpPut("{id}/endpoints/{endpointId}/toggle")]
    public async Task<IActionResult> ToggleEndpoint(string id, string endpointId, [FromBody] ToggleRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var endpoint = await _store.GetEndpointAsync(userId, id, endpointId, ct);

        if (endpoint == null)
        {
            return NotFound(new ErrorResponse($"Endpoint '{endpointId}' not found in API '{id}'"));
        }

        endpoint.IsEnabled = request.Enabled;
        await _store.UpdateEndpointAsync(endpoint, ct);

        return Ok(endpoint.ToDto());
    }

    private string GetRequiredUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated");
    }

    private async Task<string> GetUserTierAsync(CancellationToken ct)
    {
        var user = await _currentUser.GetCurrentUserAsync(ct);
        return user?.Tier ?? "free";
    }
}
