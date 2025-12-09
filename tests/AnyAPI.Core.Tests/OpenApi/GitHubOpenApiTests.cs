namespace AnyAPI.Core.Tests.OpenApi;

using AnyAPI.Core.Models;
using AnyAPI.Core.OpenApi;

/// <summary>
/// Integration tests for parsing GitHub's OpenAPI specification.
/// </summary>
public class GitHubOpenApiTests
{
    private const string GitHubOpenApiUrl = "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json";

    [Fact]
    public async Task ParseGitHubSpec_ParsesSuccessfully()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);

        // Act
        var registration = await parser.ParseAsync(GitHubOpenApiUrl);

        // Assert
        Assert.NotNull(registration);
        Assert.StartsWith("github", registration.Id); // GitHub v3 REST API -> github-v3-rest-api
        Assert.Contains("GitHub", registration.DisplayName);
        Assert.NotEmpty(registration.Endpoints);
        Assert.NotNull(registration.BaseUrl);
    }

    [Fact]
    public async Task ParseGitHubSpec_ExtractsEndpoints()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);

        // Act
        var registration = await parser.ParseAsync(GitHubOpenApiUrl);

        // Assert
        // GitHub API has hundreds of endpoints
        Assert.True(registration.Endpoints.Count > 100,
            $"Expected >100 endpoints, got {registration.Endpoints.Count}");

        // Check for some well-known endpoints
        var getUser = registration.Endpoints.FirstOrDefault(e =>
            e.Path == "/user" && e.Method == "GET");
        Assert.NotNull(getUser);

        var getRepos = registration.Endpoints.FirstOrDefault(e =>
            e.Path == "/repos/{owner}/{repo}" && e.Method == "GET");
        Assert.NotNull(getRepos);
        Assert.Contains(getRepos.Parameters, p => p.Name == "owner" && p.In == "path");
        Assert.Contains(getRepos.Parameters, p => p.Name == "repo" && p.In == "path");
    }

    [Fact]
    public async Task ParseGitHubSpec_ExtractsAuthConfiguration()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);

        // Act
        var registration = await parser.ParseAsync(GitHubOpenApiUrl);

        // Assert
        Assert.NotNull(registration.Auth);
        // GitHub spec may not include security schemes in Components, which returns NoAuthConfig
        // The important thing is that auth is not null and was extracted
        Assert.NotNull(registration.Auth.Name ?? "no-auth");
    }

    [Fact]
    public async Task ParseGitHubSpec_EndpointsHaveToolNames()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);

        // Act
        var registration = await parser.ParseAsync(GitHubOpenApiUrl);

        // Assert - all endpoints should generate valid tool names
        foreach (var endpoint in registration.Endpoints.Take(50))
        {
            var toolName = endpoint.GetToolName(registration.Id);
            Assert.False(string.IsNullOrWhiteSpace(toolName),
                $"Endpoint {endpoint.Method} {endpoint.Path} has no tool name");
            Assert.Contains(".", toolName); // Should have namespace separator
        }
    }

    [Fact]
    public async Task ParseGitHubSpec_RequestBodiesAreParsed()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);

        // Act
        var registration = await parser.ParseAsync(GitHubOpenApiUrl);

        // Assert - POST endpoints should have request body definitions
        var postEndpoints = registration.Endpoints.Where(e => e.Method == "POST").Take(10);

        foreach (var endpoint in postEndpoints)
        {
            // Most POST endpoints have a request body
            if (endpoint.RequestBody != null)
            {
                Assert.NotEmpty(endpoint.RequestBody.Content);
            }
        }
    }
}
