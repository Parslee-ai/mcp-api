namespace McpApi.Core.OpenApi;

using McpApi.Core.Models;

/// <summary>
/// Interface for parsing OpenAPI specifications.
/// </summary>
public interface IOpenApiParser
{
    /// <summary>
    /// Parses an OpenAPI spec from a URL.
    /// </summary>
    Task<ApiRegistration> ParseAsync(string specUrl, CancellationToken ct = default);

    /// <summary>
    /// Parses an OpenAPI spec from a stream.
    /// </summary>
    Task<ApiRegistration> ParseAsync(Stream specStream, string baseUrl, CancellationToken ct = default);
}
