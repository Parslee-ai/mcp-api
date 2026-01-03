using System.ComponentModel.DataAnnotations;
using McpApi.Core.Models;

namespace McpApi.Api.DTOs;

public record RegisterApiRequest(
    [Required] string BaseUrl,
    string? SpecUrl = null
);

public record UpdateApiRequest(
    string? DisplayName,
    bool? IsEnabled
);

public record ToggleRequest(bool Enabled);

public record UpdateAuthConfigRequest(
    [Required] string AuthType,
    string? Name,
    // For ApiKey
    string? In,
    string? ParameterName,
    string? ApiKeyValue,
    // For Bearer
    string? BearerToken,
    string? Prefix,
    // For Basic
    string? Username,
    string? Password,
    // For OAuth2
    string? Flow,
    string? TokenUrl,
    string? AuthorizationUrl,
    string? ClientId,
    string? ClientSecret,
    List<string>? Scopes
);

public record ApiRegistrationDto(
    string Id,
    string DisplayName,
    string BaseUrl,
    string? SpecUrl,
    string OpenApiVersion,
    string? ApiVersion,
    string? Description,
    string AuthType,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime LastRefreshed,
    int EndpointCount,
    int EnabledEndpointCount
);

public record ApiEndpointDto(
    string Id,
    string ApiId,
    string OperationId,
    string Method,
    string Path,
    string? Summary,
    string? Description,
    List<string> Tags,
    bool IsEnabled
);

public record ApiDetailDto(
    string Id,
    string DisplayName,
    string BaseUrl,
    string? SpecUrl,
    string OpenApiVersion,
    string? ApiVersion,
    string? Description,
    AuthConfigDto Auth,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime LastRefreshed,
    List<ApiEndpointDto> Endpoints
);

public record AuthConfigDto(
    string AuthType,
    string? Name,
    string? In,
    string? ParameterName,
    string? Prefix
);

public static class ApiDtoExtensions
{
    public static ApiRegistrationDto ToDto(this ApiRegistration api, int endpointCount, int enabledEndpointCount)
    {
        return new ApiRegistrationDto(
            api.Id,
            api.DisplayName,
            api.BaseUrl,
            api.SpecUrl,
            api.OpenApiVersion,
            api.ApiVersion,
            api.Description,
            api.Auth.AuthType,
            api.IsEnabled,
            api.CreatedAt,
            api.LastRefreshed,
            endpointCount,
            enabledEndpointCount
        );
    }

    public static ApiEndpointDto ToDto(this ApiEndpoint endpoint)
    {
        return new ApiEndpointDto(
            endpoint.Id,
            endpoint.ApiId,
            endpoint.OperationId,
            endpoint.Method,
            endpoint.Path,
            endpoint.Summary,
            endpoint.Description,
            endpoint.Tags,
            endpoint.IsEnabled
        );
    }

    public static AuthConfigDto ToDto(this AuthConfiguration auth)
    {
        return auth switch
        {
            ApiKeyAuthConfig apiKey => new AuthConfigDto(
                apiKey.AuthType,
                apiKey.Name,
                apiKey.In,
                apiKey.ParameterName,
                null
            ),
            BearerTokenAuthConfig bearer => new AuthConfigDto(
                bearer.AuthType,
                bearer.Name,
                null,
                null,
                bearer.Prefix
            ),
            BasicAuthConfig basic => new AuthConfigDto(
                basic.AuthType,
                basic.Name,
                null,
                null,
                null
            ),
            OAuth2AuthConfig oauth => new AuthConfigDto(
                oauth.AuthType,
                oauth.Name,
                null,
                null,
                null
            ),
            _ => new AuthConfigDto(auth.AuthType, auth.Name, null, null, null)
        };
    }

    public static ApiDetailDto ToDetailDto(this ApiRegistration api, IReadOnlyList<ApiEndpoint> endpoints)
    {
        return new ApiDetailDto(
            api.Id,
            api.DisplayName,
            api.BaseUrl,
            api.SpecUrl,
            api.OpenApiVersion,
            api.ApiVersion,
            api.Description,
            api.Auth.ToDto(),
            api.IsEnabled,
            api.CreatedAt,
            api.LastRefreshed,
            endpoints.Select(e => e.ToDto()).ToList()
        );
    }
}
