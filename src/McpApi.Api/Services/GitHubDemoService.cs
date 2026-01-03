using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpApi.Api.Services;

public interface IGitHubDemoService
{
    Task<GitHubIssue> CreateIssueAsync(string title, string? body = null, string[]? labels = null);
    Task<GitHubIssue[]> ListIssuesAsync(string? state = null, int? limit = null);
    Task<GitHubIssue> GetIssueAsync(int issueNumber);
    Task<GitHubComment> AddCommentAsync(int issueNumber, string body);
    Task<GitHubIssue> CloseIssueAsync(int issueNumber);
    Task<GitHubIssue[]> SearchIssuesAsync(string query);
}

public class GitHubDemoService : IGitHubDemoService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ILogger<GitHubDemoService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public GitHubDemoService(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubDemoService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var token = configuration["GitHub:DemoToken"]
            ?? configuration["github-demo-token"]
            ?? throw new InvalidOperationException("GitHub demo token not configured");

        _owner = configuration["GitHub:DemoOwner"] ?? "Parslee-ai";
        _repo = configuration["GitHub:DemoRepo"] ?? "mcp-api-demo";

        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MCP-API-Demo", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<GitHubIssue> CreateIssueAsync(string title, string? body = null, string[]? labels = null)
    {
        var payload = new
        {
            title,
            body = body ?? $"This issue was created via MCP-API demo at {DateTime.UtcNow:u}",
            labels = labels ?? new[] { "demo", "mcp-api" }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"repos/{_owner}/{_repo}/issues", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("GitHub API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubIssue>(json, JsonOptions)!;
    }

    public async Task<GitHubIssue[]> ListIssuesAsync(string? state = null, int? limit = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(state)) query.Add($"state={state}");
        if (limit.HasValue) query.Add($"per_page={Math.Min(limit.Value, 10)}");
        else query.Add("per_page=5");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        var response = await _httpClient.GetAsync($"repos/{_owner}/{_repo}/issues{queryString}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("GitHub API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubIssue[]>(json, JsonOptions) ?? [];
    }

    public async Task<GitHubIssue> GetIssueAsync(int issueNumber)
    {
        var response = await _httpClient.GetAsync($"repos/{_owner}/{_repo}/issues/{issueNumber}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("GitHub API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubIssue>(json, JsonOptions)!;
    }

    public async Task<GitHubComment> AddCommentAsync(int issueNumber, string body)
    {
        var payload = new { body };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"repos/{_owner}/{_repo}/issues/{issueNumber}/comments", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("GitHub API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubComment>(json, JsonOptions)!;
    }

    public async Task<GitHubIssue> CloseIssueAsync(int issueNumber)
    {
        var payload = new { state = "closed" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync($"repos/{_owner}/{_repo}/issues/{issueNumber}", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("GitHub API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubIssue>(json, JsonOptions)!;
    }

    public async Task<GitHubIssue[]> SearchIssuesAsync(string query)
    {
        var searchQuery = Uri.EscapeDataString($"{query} repo:{_owner}/{_repo}");
        var response = await _httpClient.GetAsync($"search/issues?q={searchQuery}&per_page=5");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("GitHub API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GitHubSearchResult>(json, JsonOptions);
        return result?.Items ?? [];
    }
}

public class GitHubIssue
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string State { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public GitHubUser? User { get; set; }
    public string[] Labels { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class GitHubUser
{
    public string Login { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
}

public class GitHubComment
{
    public int Id { get; set; }
    public string Body { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public GitHubUser? User { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GitHubSearchResult
{
    public int TotalCount { get; set; }
    public GitHubIssue[] Items { get; set; } = [];
}
