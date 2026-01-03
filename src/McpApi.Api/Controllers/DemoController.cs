using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using McpApi.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace McpApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IGitHubDemoService _githubService;
    private readonly HttpClient _httpClient;
    private readonly string? _anthropicApiKey;
    private readonly ILogger<DemoController> _logger;
    private readonly bool _demoEnabled;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DemoController(
        IGitHubDemoService githubService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DemoController> logger)
    {
        _githubService = githubService;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _demoEnabled = configuration.GetValue<bool>("Demo:Enabled", true);
        _anthropicApiKey = configuration["Anthropic:ApiKey"] ?? configuration["anthropic-api-key"];
    }

    [HttpPost("chat")]
    public async Task<ActionResult<DemoChatResponse>> Chat([FromBody] DemoChatRequest request)
    {
        if (!_demoEnabled || string.IsNullOrEmpty(_anthropicApiKey) || _githubService == null)
        {
            return BadRequest(new { error = "Demo is currently disabled" });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        if (request.Message.Length > 500)
        {
            return BadRequest(new { error = "Message too long (max 500 characters)" });
        }

        try
        {
            var tools = GetGitHubTools();
            var messages = new List<JsonObject>
            {
                new JsonObject { ["role"] = "user", ["content"] = request.Message }
            };

            var systemPrompt = @"You are a helpful assistant demonstrating MCP-API's ability to interact with the GitHub API.
You have access to tools that can create issues, list issues, add comments, and more on the demo repository.

When users ask you to perform GitHub actions:
1. Use the appropriate tool to complete the action
2. Provide a clear, concise response about what you did
3. Always include the link to the created/modified issue so they can verify

Keep responses brief and focused. This is a demo, so be friendly and encourage users to try different actions.

The demo repository is: Parslee-ai/mcp-api-demo";

            var response = await ExecuteWithToolsAsync(messages, tools, systemPrompt);

            return Ok(new DemoChatResponse
            {
                Message = response.Message,
                Actions = response.Actions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in demo chat");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }

    private async Task<(string Message, List<DemoAction> Actions)> ExecuteWithToolsAsync(
        List<JsonObject> messages,
        JsonArray tools,
        string systemPrompt)
    {
        var actions = new List<DemoAction>();
        var maxIterations = 5;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;

            var requestBody = new JsonObject
            {
                ["model"] = "claude-sonnet-4-20250514",
                ["max_tokens"] = 1024,
                ["system"] = systemPrompt,
                ["messages"] = new JsonArray(messages.Select(m => JsonNode.Parse(m.ToJsonString())).ToArray()),
                ["tools"] = JsonNode.Parse(tools.ToJsonString())
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("x-api-key", _anthropicApiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var httpResponse = await _httpClient.SendAsync(httpRequest);
            var responseJson = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error: {StatusCode} - {Response}", httpResponse.StatusCode, responseJson);
                throw new HttpRequestException($"Anthropic API error: {httpResponse.StatusCode}");
            }

            var response = JsonNode.Parse(responseJson)!;
            var stopReason = response["stop_reason"]?.GetValue<string>();
            var content = response["content"]?.AsArray();

            if (stopReason == "end_turn" || stopReason == "stop_sequence")
            {
                var textContent = content?.FirstOrDefault(c => c?["type"]?.GetValue<string>() == "text");
                return (textContent?["text"]?.GetValue<string>() ?? "I completed the requested action.", actions);
            }

            if (stopReason == "tool_use")
            {
                var assistantContent = new JsonArray();
                var toolResults = new JsonArray();

                foreach (var item in content ?? [])
                {
                    assistantContent.Add(JsonNode.Parse(item!.ToJsonString()));

                    if (item?["type"]?.GetValue<string>() == "tool_use")
                    {
                        var toolName = item["name"]?.GetValue<string>() ?? "";
                        var toolId = item["id"]?.GetValue<string>() ?? "";
                        var input = item["input"]?.AsObject();

                        var inputDict = input != null
                            ? JsonSerializer.Deserialize<Dictionary<string, object>>(input.ToJsonString())
                            : new Dictionary<string, object>();

                        var result = await ExecuteToolAsync(toolName, inputDict);

                        actions.Add(new DemoAction
                        {
                            Tool = toolName,
                            Input = inputDict,
                            Output = result.Output,
                            Url = result.Url
                        });

                        toolResults.Add(new JsonObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = toolId,
                            ["content"] = result.Output
                        });
                    }
                }

                messages.Add(new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = JsonNode.Parse(assistantContent.ToJsonString())
                });
                messages.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = JsonNode.Parse(toolResults.ToJsonString())
                });
            }
            else
            {
                break;
            }
        }

        return ("I wasn't able to complete the request. Please try again.", actions);
    }

    private async Task<(string Output, string? Url)> ExecuteToolAsync(string toolName, Dictionary<string, object>? input)
    {
        try
        {
            input ??= new Dictionary<string, object>();

            switch (toolName)
            {
                case "create_issue":
                {
                    var title = GetStringValue(input, "title") ?? "Untitled";
                    var body = GetStringValue(input, "body");
                    string[]? labels = null;
                    if (input.TryGetValue("labels", out var l) && l is JsonElement je && je.ValueKind == JsonValueKind.Array)
                    {
                        labels = je.EnumerateArray().Select(x => x.GetString()!).ToArray();
                    }

                    var issue = await _githubService.CreateIssueAsync(title, body, labels);
                    return (JsonSerializer.Serialize(new
                    {
                        success = true,
                        issue = new { issue.Number, issue.Title, issue.State, issue.HtmlUrl }
                    }, JsonOptions), issue.HtmlUrl);
                }

                case "list_issues":
                {
                    var state = GetStringValue(input, "state");
                    int? limit = GetIntValue(input, "limit");

                    var issues = await _githubService.ListIssuesAsync(state, limit);
                    return (JsonSerializer.Serialize(new
                    {
                        success = true,
                        count = issues.Length,
                        issues = issues.Select(i => new { i.Number, i.Title, i.State, i.HtmlUrl })
                    }, JsonOptions), null);
                }

                case "get_issue":
                {
                    var number = GetIntValue(input, "issue_number") ?? 0;
                    var issue = await _githubService.GetIssueAsync(number);
                    return (JsonSerializer.Serialize(new
                    {
                        success = true,
                        issue = new { issue.Number, issue.Title, issue.Body, issue.State, issue.HtmlUrl }
                    }, JsonOptions), issue.HtmlUrl);
                }

                case "add_comment":
                {
                    var number = GetIntValue(input, "issue_number") ?? 0;
                    var body = GetStringValue(input, "body") ?? "";
                    var comment = await _githubService.AddCommentAsync(number, body);
                    return (JsonSerializer.Serialize(new
                    {
                        success = true,
                        comment = new { comment.Id, comment.Body, comment.HtmlUrl }
                    }, JsonOptions), comment.HtmlUrl);
                }

                case "close_issue":
                {
                    var number = GetIntValue(input, "issue_number") ?? 0;
                    var issue = await _githubService.CloseIssueAsync(number);
                    return (JsonSerializer.Serialize(new
                    {
                        success = true,
                        issue = new { issue.Number, issue.Title, issue.State, issue.HtmlUrl }
                    }, JsonOptions), issue.HtmlUrl);
                }

                case "search_issues":
                {
                    var query = GetStringValue(input, "query") ?? "";
                    var issues = await _githubService.SearchIssuesAsync(query);
                    return (JsonSerializer.Serialize(new
                    {
                        success = true,
                        count = issues.Length,
                        issues = issues.Select(i => new { i.Number, i.Title, i.State, i.HtmlUrl })
                    }, JsonOptions), null);
                }

                default:
                    return ($"Unknown tool: {toolName}", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return (JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions), null);
        }
    }

    private static string? GetStringValue(Dictionary<string, object> input, string key)
    {
        if (!input.TryGetValue(key, out var value)) return null;
        return value switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => value?.ToString()
        };
    }

    private static int? GetIntValue(Dictionary<string, object> input, string key)
    {
        if (!input.TryGetValue(key, out var value)) return null;
        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static JsonArray GetGitHubTools()
    {
        return new JsonArray
        {
            new JsonObject
            {
                ["name"] = "create_issue",
                ["description"] = "Create a new issue in the demo GitHub repository",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["title"] = new JsonObject { ["type"] = "string", ["description"] = "The title of the issue" },
                        ["body"] = new JsonObject { ["type"] = "string", ["description"] = "The body/description of the issue (optional)" },
                        ["labels"] = new JsonObject { ["type"] = "array", ["description"] = "Labels to add to the issue (optional)", ["items"] = new JsonObject { ["type"] = "string" } }
                    },
                    ["required"] = new JsonArray { "title" }
                }
            },
            new JsonObject
            {
                ["name"] = "list_issues",
                ["description"] = "List issues in the demo repository",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["state"] = new JsonObject { ["type"] = "string", ["description"] = "Filter by state: 'open', 'closed', or 'all'" },
                        ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "Maximum number of issues to return (max 10)" }
                    },
                    ["required"] = new JsonArray()
                }
            },
            new JsonObject
            {
                ["name"] = "get_issue",
                ["description"] = "Get details of a specific issue by number",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["issue_number"] = new JsonObject { ["type"] = "integer", ["description"] = "The issue number" }
                    },
                    ["required"] = new JsonArray { "issue_number" }
                }
            },
            new JsonObject
            {
                ["name"] = "add_comment",
                ["description"] = "Add a comment to an existing issue",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["issue_number"] = new JsonObject { ["type"] = "integer", ["description"] = "The issue number to comment on" },
                        ["body"] = new JsonObject { ["type"] = "string", ["description"] = "The comment text" }
                    },
                    ["required"] = new JsonArray { "issue_number", "body" }
                }
            },
            new JsonObject
            {
                ["name"] = "close_issue",
                ["description"] = "Close an open issue",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["issue_number"] = new JsonObject { ["type"] = "integer", ["description"] = "The issue number to close" }
                    },
                    ["required"] = new JsonArray { "issue_number" }
                }
            },
            new JsonObject
            {
                ["name"] = "search_issues",
                ["description"] = "Search for issues by keyword",
                ["input_schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Search query" }
                    },
                    ["required"] = new JsonArray { "query" }
                }
            }
        };
    }
}

public class DemoChatRequest
{
    public string Message { get; set; } = "";
}

public class DemoChatResponse
{
    public string Message { get; set; } = "";
    public List<DemoAction> Actions { get; set; } = [];
}

public class DemoAction
{
    public string Tool { get; set; } = "";
    public object? Input { get; set; }
    public string Output { get; set; } = "";
    public string? Url { get; set; }
}
