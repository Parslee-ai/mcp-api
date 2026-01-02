namespace McpApi.Core.Tests.Http;

using McpApi.Core.Http;
using McpApi.Core.Models;
using System.Text.Json;
using Xunit;

public class RequestBuilderTests
{
    private const string BaseUrl = "https://api.example.com";

    #region Path Parameter Tests

    [Fact]
    public void Build_WithPathParameters_SubstitutesInPath()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users/{userId}/posts/{postId}",
            CreateParam("userId", "path", required: true),
            CreateParam("postId", "path", required: true));

        var parameters = new Dictionary<string, object?>
        {
            ["userId"] = "123",
            ["postId"] = "456"
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.Equal("https://api.example.com/users/123/posts/456", request.RequestUri!.ToString());
    }

    [Fact]
    public void Build_WithPathParameters_UrlEncodesValues()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/search/{query}",
            CreateParam("query", "path", required: true));

        var parameters = new Dictionary<string, object?>
        {
            ["query"] = "test&value"
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert - special chars should be encoded
        var uri = request.RequestUri!.ToString();
        Assert.Contains("/search/", uri);
        Assert.DoesNotContain("&value", uri); // & should be encoded
        Assert.Contains("%26", uri); // & becomes %26
    }

    #endregion

    #region Query Parameter Tests

    [Fact]
    public void Build_WithQueryParameters_AddsToQueryString()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users",
            CreateParam("page", "query"),
            CreateParam("limit", "query"));

        var parameters = new Dictionary<string, object?>
        {
            ["page"] = 1,
            ["limit"] = 20
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.Contains("page=1", request.RequestUri!.Query);
        Assert.Contains("limit=20", request.RequestUri!.Query);
    }

    [Fact]
    public void Build_WithOptionalQueryParameter_OmitsIfNotProvided()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users",
            CreateParam("page", "query"),
            CreateParam("filter", "query"));

        var parameters = new Dictionary<string, object?>
        {
            ["page"] = 1
            // filter not provided
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.Contains("page=1", request.RequestUri!.Query);
        Assert.DoesNotContain("filter", request.RequestUri!.Query);
    }

    #endregion

    #region Header Parameter Tests

    [Fact]
    public void Build_WithHeaderParameters_AddsHeaders()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users",
            CreateParam("X-Request-Id", "header"),
            CreateParam("X-Correlation-Id", "header"));

        var parameters = new Dictionary<string, object?>
        {
            ["X-Request-Id"] = "req-123",
            ["X-Correlation-Id"] = "corr-456"
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.True(request.Headers.TryGetValues("X-Request-Id", out var reqId));
        Assert.Equal("req-123", reqId.Single());
        Assert.True(request.Headers.TryGetValues("X-Correlation-Id", out var corrId));
        Assert.Equal("corr-456", corrId.Single());
    }

    #endregion

    #region Request Body Tests

    [Fact]
    public async Task Build_WithExplicitBody_SetsJsonContent()
    {
        // Arrange
        var endpoint = CreateEndpoint("POST", "/users");
        endpoint.RequestBody = new RequestBodyDefinition { Required = true };

        var parameters = new Dictionary<string, object?>
        {
            ["body"] = new { name = "John", email = "john@example.com" }
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.NotNull(request.Content);
        var content = await request.Content.ReadAsStringAsync();
        Assert.Contains("\"name\"", content);
        Assert.Contains("\"email\"", content);
    }

    [Fact]
    public async Task Build_WithStringBody_SetsStringContent()
    {
        // Arrange
        var endpoint = CreateEndpoint("POST", "/users");
        endpoint.RequestBody = new RequestBodyDefinition { Required = true };

        var parameters = new Dictionary<string, object?>
        {
            ["body"] = "{\"name\":\"John\"}"
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        var content = await request.Content!.ReadAsStringAsync();
        Assert.Equal("{\"name\":\"John\"}", content);
    }

    [Fact]
    public async Task Build_WithBodyParams_BuildsBodyFromParams()
    {
        // Arrange
        var endpoint = CreateEndpoint("POST", "/users",
            CreateParam("userId", "path", required: true));
        endpoint.RequestBody = new RequestBodyDefinition { Required = false };

        var parameters = new Dictionary<string, object?>
        {
            ["userId"] = "123", // path param - should not be in body
            ["name"] = "John",  // should be in body
            ["email"] = "john@example.com" // should be in body
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.NotNull(request.Content);
        var content = await request.Content.ReadAsStringAsync();
        Assert.Contains("name", content);
        Assert.Contains("email", content);
        Assert.DoesNotContain("userId", content);
    }

    [Fact]
    public void Build_WithGetMethod_DoesNotAddBody()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users");

        var parameters = new Dictionary<string, object?>
        {
            ["body"] = new { name = "John" }
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.Null(request.Content);
    }

    #endregion

    #region Required Parameter Validation Tests

    [Fact]
    public void Build_WithMissingRequiredPathParam_ThrowsArgumentException()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users/{userId}",
            CreateParam("userId", "path", required: true));

        var parameters = new Dictionary<string, object?>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RequestBuilder.Build(BaseUrl, endpoint, parameters));
        Assert.Contains("userId", ex.Message);
        Assert.Contains("Missing required", ex.Message);
    }

    [Fact]
    public void Build_WithMissingRequiredQueryParam_ThrowsArgumentException()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/search",
            CreateParam("q", "query", required: true));

        var parameters = new Dictionary<string, object?>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RequestBuilder.Build(BaseUrl, endpoint, parameters));
        Assert.Contains("q", ex.Message);
    }

    [Fact]
    public void Build_WithNullRequiredParam_ThrowsArgumentException()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users/{userId}",
            CreateParam("userId", "path", required: true));

        var parameters = new Dictionary<string, object?>
        {
            ["userId"] = null
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            RequestBuilder.Build(BaseUrl, endpoint, parameters));
    }

    [Fact]
    public void Build_WithMissingRequiredBody_ThrowsArgumentException()
    {
        // Arrange
        var endpoint = CreateEndpoint("POST", "/users");
        endpoint.RequestBody = new RequestBodyDefinition { Required = true };

        var parameters = new Dictionary<string, object?>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            RequestBuilder.Build(BaseUrl, endpoint, parameters));
        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region HTTP Method Tests

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Build_WithVariousMethods_SetsCorrectMethod(string method)
    {
        // Arrange
        var endpoint = CreateEndpoint(method, "/users");
        var parameters = new Dictionary<string, object?>();

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.Equal(method, request.Method.Method);
    }

    #endregion

    #region URL Building Tests

    [Fact]
    public void Build_WithBaseUrlTrailingSlash_BuildsCorrectUrl()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users");
        var parameters = new Dictionary<string, object?>();

        // Act
        var request = RequestBuilder.Build("https://api.example.com/", endpoint, parameters);

        // Assert
        Assert.Equal("https://api.example.com/users", request.RequestUri!.ToString());
    }

    [Fact]
    public void Build_CombinesPathAndQueryParameters()
    {
        // Arrange
        var endpoint = CreateEndpoint("GET", "/users/{userId}/posts",
            CreateParam("userId", "path", required: true),
            CreateParam("page", "query"));

        var parameters = new Dictionary<string, object?>
        {
            ["userId"] = "42",
            ["page"] = 1
        };

        // Act
        var request = RequestBuilder.Build(BaseUrl, endpoint, parameters);

        // Assert
        Assert.Equal("https://api.example.com/users/42/posts?page=1", request.RequestUri!.ToString());
    }

    #endregion

    #region Helper Methods

    private static ApiEndpoint CreateEndpoint(string method, string path, params ParameterDefinition[] parameters)
    {
        return new ApiEndpoint
        {
            Id = "test-endpoint",
            OperationId = "testOperation",
            Method = method,
            Path = path,
            Parameters = parameters.ToList()
        };
    }

    private static ParameterDefinition CreateParam(string name, string location, bool required = false)
    {
        return new ParameterDefinition
        {
            Name = name,
            In = location,
            Required = required,
            Schema = new JsonSchema { Type = "string" }
        };
    }

    #endregion
}
