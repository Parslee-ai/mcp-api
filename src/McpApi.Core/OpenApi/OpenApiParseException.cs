namespace McpApi.Core.OpenApi;

/// <summary>
/// Exception thrown when OpenAPI parsing fails.
/// </summary>
public class OpenApiParseException : Exception
{
    public OpenApiParseException(string message) : base(message) { }
    public OpenApiParseException(string message, Exception inner) : base(message, inner) { }
}
