namespace AnyAPI.Core.Http;

/// <summary>
/// Wrapper for API responses.
/// </summary>
public class ApiResponse
{
    /// <summary>HTTP status code.</summary>
    public required int StatusCode { get; init; }

    /// <summary>HTTP reason phrase.</summary>
    public required string? ReasonPhrase { get; init; }

    /// <summary>Response headers.</summary>
    public required Dictionary<string, string[]> Headers { get; init; }

    /// <summary>Response body as string.</summary>
    public required string? Body { get; init; }

    /// <summary>Whether the response indicates success (2xx).</summary>
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
}
