namespace McpApi.Core.OpenApi;

using McpApi.Core.Models;
using Microsoft.OpenApi.Models;

/// <summary>
/// Converts OpenAPI operations to ApiEndpoint models.
/// </summary>
public class OperationConverter
{
    private readonly SchemaFlattener _schemaFlattener;

    public OperationConverter(SchemaFlattener? schemaFlattener = null)
    {
        _schemaFlattener = schemaFlattener ?? new SchemaFlattener();
    }

    /// <summary>
    /// Converts an OpenAPI operation to an ApiEndpoint.
    /// </summary>
    public ApiEndpoint Convert(
        string path,
        string method,
        OpenApiOperation operation,
        IList<OpenApiParameter>? pathParameters = null)
    {
        var operationId = operation.OperationId ?? GenerateOperationId(method, path);

        var endpoint = new ApiEndpoint
        {
            Id = operationId.ToLowerInvariant().Replace("_", "-"),
            OperationId = operationId,
            Method = method.ToUpperInvariant(),
            Path = path,
            Summary = operation.Summary,
            Description = operation.Description,
            Tags = operation.Tags?.Select(t => t.Name).ToList() ?? [],
            Parameters = [],
            Responses = new Dictionary<string, ResponseDefinition>()
        };

        // Merge path-level and operation-level parameters
        var allParams = (pathParameters ?? [])
            .Concat(operation.Parameters ?? [])
            .DistinctBy(p => $"{p.In}:{p.Name}");

        foreach (var param in allParams)
        {
            endpoint.Parameters.Add(ConvertParameter(param));
        }

        // Convert request body
        if (operation.RequestBody != null)
        {
            endpoint.RequestBody = ConvertRequestBody(operation.RequestBody);
        }

        // Convert responses
        foreach (var (statusCode, response) in operation.Responses ?? new OpenApiResponses())
        {
            endpoint.Responses[statusCode] = ConvertResponse(response);
        }

        return endpoint;
    }

    private ParameterDefinition ConvertParameter(OpenApiParameter param)
    {
        return new ParameterDefinition
        {
            Name = param.Name,
            In = param.In?.ToString().ToLowerInvariant() ?? "query",
            Required = param.Required,
            Description = param.Description,
            Schema = _schemaFlattener.Flatten(param.Schema),
            Example = param.Example?.ToString(),
            Default = param.Schema?.Default?.ToString()
        };
    }

    private RequestBodyDefinition ConvertRequestBody(OpenApiRequestBody body)
    {
        var definition = new RequestBodyDefinition
        {
            Required = body.Required,
            Description = body.Description,
            Content = new Dictionary<string, JsonSchema>()
        };

        foreach (var (mediaType, content) in body.Content ?? new Dictionary<string, OpenApiMediaType>())
        {
            definition.Content[mediaType] = _schemaFlattener.Flatten(content.Schema);
        }

        return definition;
    }

    private ResponseDefinition ConvertResponse(OpenApiResponse response)
    {
        var definition = new ResponseDefinition
        {
            Description = response.Description,
            Content = new Dictionary<string, JsonSchema>()
        };

        foreach (var (mediaType, content) in response.Content ?? new Dictionary<string, OpenApiMediaType>())
        {
            definition.Content[mediaType] = _schemaFlattener.Flatten(content.Schema);
        }

        return definition;
    }

    private static string GenerateOperationId(string method, string path)
    {
        // /repos/{owner}/{repo}/issues -> repos_owner_repo_issues
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.StartsWith('{') ? p.Trim('{', '}') : p);

        return $"{method.ToLowerInvariant()}_{string.Join("_", parts)}";
    }
}
