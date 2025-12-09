namespace AnyAPI.Core.GraphQL;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnyAPI.Core.Models;

/// <summary>
/// Parses GraphQL schemas into ApiRegistration.
/// Supports both introspection queries and SDL (Schema Definition Language).
/// </summary>
public partial class GraphQLSchemaParser
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Standard GraphQL introspection query
    private const string IntrospectionQuery = """
        query IntrospectionQuery {
          __schema {
            queryType { name }
            mutationType { name }
            subscriptionType { name }
            types {
              kind
              name
              description
              fields(includeDeprecated: true) {
                name
                description
                args {
                  name
                  description
                  type { ...TypeRef }
                  defaultValue
                }
                type { ...TypeRef }
                isDeprecated
                deprecationReason
              }
              inputFields {
                name
                description
                type { ...TypeRef }
                defaultValue
              }
              interfaces { ...TypeRef }
              enumValues(includeDeprecated: true) {
                name
                description
                isDeprecated
                deprecationReason
              }
              possibleTypes { ...TypeRef }
            }
          }
        }

        fragment TypeRef on __Type {
          kind
          name
          ofType {
            kind
            name
            ofType {
              kind
              name
              ofType {
                kind
                name
                ofType {
                  kind
                  name
                }
              }
            }
          }
        }
        """;

    public GraphQLSchemaParser(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Checks if content looks like a GraphQL schema (SDL format).
    /// </summary>
    public static bool IsGraphQLSchema(string content)
    {
        // Check for SDL keywords
        var sdlKeywords = new[] { "type Query", "type Mutation", "schema {", "directive @" };
        return sdlKeywords.Any(kw => content.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a URL might be a GraphQL endpoint.
    /// </summary>
    public static bool LooksLikeGraphQLEndpoint(string url)
    {
        return url.Contains("/graphql", StringComparison.OrdinalIgnoreCase) ||
               url.EndsWith("/gql", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a GraphQL API by introspecting the endpoint.
    /// </summary>
    public async Task<ApiRegistration> ParseFromEndpointAsync(string graphqlUrl, CancellationToken ct = default)
    {
        var introspectionResult = await IntrospectAsync(graphqlUrl, ct);
        return ConvertIntrospectionToRegistration(introspectionResult, graphqlUrl);
    }

    /// <summary>
    /// Parses a GraphQL SDL schema.
    /// </summary>
    public ApiRegistration ParseFromSdl(string sdl, string baseUrl, string? name = null)
    {
        var types = ParseSdlTypes(sdl);
        return ConvertSdlToRegistration(types, baseUrl, name ?? "GraphQL API");
    }

    private async Task<IntrospectionResult> IntrospectAsync(string graphqlUrl, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, graphqlUrl)
        {
            Content = JsonContent.Create(new { query = IntrospectionQuery })
        };
        request.Headers.Add("Accept", "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<GraphQLResponse<IntrospectionData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse introspection response");

        if (result.Errors?.Count > 0)
        {
            throw new InvalidOperationException(
                $"GraphQL introspection errors: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        }

        return result.Data?.Schema
            ?? throw new InvalidOperationException("No schema in introspection response");
    }

    private ApiRegistration ConvertIntrospectionToRegistration(IntrospectionResult schema, string graphqlUrl)
    {
        var id = GenerateId(graphqlUrl);
        var name = ExtractNameFromUrl(graphqlUrl);

        var registration = new ApiRegistration
        {
            Id = id,
            DisplayName = name,
            Description = "GraphQL API",
            BaseUrl = graphqlUrl,
            SpecUrl = graphqlUrl,
            OpenApiVersion = "graphql",
            Auth = new NoAuthConfig(),
            CreatedAt = DateTime.UtcNow,
            LastRefreshed = DateTime.UtcNow,
            Endpoints = []
        };

        // Convert Query type fields to endpoints
        if (schema.QueryType?.Name != null)
        {
            var queryType = schema.Types?.FirstOrDefault(t => t.Name == schema.QueryType.Name);
            if (queryType?.Fields != null)
            {
                foreach (var field in queryType.Fields.Where(f => !f.Name.StartsWith("__")))
                {
                    registration.Endpoints.Add(CreateQueryEndpoint(field));
                }
            }
        }

        // Convert Mutation type fields to endpoints
        if (schema.MutationType?.Name != null)
        {
            var mutationType = schema.Types?.FirstOrDefault(t => t.Name == schema.MutationType.Name);
            if (mutationType?.Fields != null)
            {
                foreach (var field in mutationType.Fields.Where(f => !f.Name.StartsWith("__")))
                {
                    registration.Endpoints.Add(CreateMutationEndpoint(field));
                }
            }
        }

        return registration;
    }

    private static ApiEndpoint CreateQueryEndpoint(GraphQLField field)
    {
        var endpoint = new ApiEndpoint
        {
            Id = $"query-{field.Name}",
            OperationId = $"query-{field.Name}",
            Method = "POST", // GraphQL uses POST for all operations
            Path = $"/graphql#query.{field.Name}",
            Summary = field.Name,
            Description = field.Description ?? $"Query: {field.Name}",
            Tags = ["Query"],
            Parameters = [],
            Responses = new Dictionary<string, ResponseDefinition>
            {
                ["200"] = new() { Description = FormatTypeRef(field.Type) }
            }
        };

        // Convert args to parameters
        if (field.Args != null)
        {
            foreach (var arg in field.Args)
            {
                endpoint.Parameters.Add(new ParameterDefinition
                {
                    Name = arg.Name,
                    In = "body", // GraphQL args go in request body
                    Required = IsNonNullType(arg.Type),
                    Description = arg.Description,
                    Schema = ConvertGraphQLTypeToJsonSchema(arg.Type)
                });
            }
        }

        return endpoint;
    }

    private static ApiEndpoint CreateMutationEndpoint(GraphQLField field)
    {
        var endpoint = new ApiEndpoint
        {
            Id = $"mutation-{field.Name}",
            OperationId = $"mutation-{field.Name}",
            Method = "POST",
            Path = $"/graphql#mutation.{field.Name}",
            Summary = field.Name,
            Description = field.Description ?? $"Mutation: {field.Name}",
            Tags = ["Mutation"],
            Parameters = [],
            Responses = new Dictionary<string, ResponseDefinition>
            {
                ["200"] = new() { Description = FormatTypeRef(field.Type) }
            }
        };

        // Convert args to parameters
        if (field.Args != null)
        {
            foreach (var arg in field.Args)
            {
                endpoint.Parameters.Add(new ParameterDefinition
                {
                    Name = arg.Name,
                    In = "body",
                    Required = IsNonNullType(arg.Type),
                    Description = arg.Description,
                    Schema = ConvertGraphQLTypeToJsonSchema(arg.Type)
                });
            }
        }

        return endpoint;
    }

    private static bool IsNonNullType(GraphQLTypeRef? type)
    {
        return type?.Kind == "NON_NULL";
    }

    private static string FormatTypeRef(GraphQLTypeRef? type)
    {
        if (type == null) return "Unknown";

        return type.Kind switch
        {
            "NON_NULL" => $"{FormatTypeRef(type.OfType)}!",
            "LIST" => $"[{FormatTypeRef(type.OfType)}]",
            _ => type.Name ?? "Unknown"
        };
    }

    private static JsonSchema ConvertGraphQLTypeToJsonSchema(GraphQLTypeRef? type)
    {
        if (type == null) return new JsonSchema { Type = "object" };

        var innerType = UnwrapType(type);
        var isList = IsListType(type);

        var baseSchema = innerType?.Name?.ToUpperInvariant() switch
        {
            "STRING" or "ID" => new JsonSchema { Type = "string" },
            "INT" => new JsonSchema { Type = "integer" },
            "FLOAT" => new JsonSchema { Type = "number" },
            "BOOLEAN" => new JsonSchema { Type = "boolean" },
            _ => new JsonSchema { Type = "object", Description = innerType?.Name }
        };

        if (isList)
        {
            return new JsonSchema { Type = "array", Items = baseSchema };
        }

        return baseSchema;
    }

    private static GraphQLTypeRef? UnwrapType(GraphQLTypeRef? type)
    {
        while (type != null && (type.Kind == "NON_NULL" || type.Kind == "LIST"))
        {
            type = type.OfType;
        }
        return type;
    }

    private static bool IsListType(GraphQLTypeRef? type)
    {
        while (type != null)
        {
            if (type.Kind == "LIST") return true;
            type = type.OfType;
        }
        return false;
    }

    private List<SdlType> ParseSdlTypes(string sdl)
    {
        var types = new List<SdlType>();

        // Parse type definitions
        var typeMatches = TypeDefinitionRegex().Matches(sdl);
        foreach (Match match in typeMatches)
        {
            var typeName = match.Groups[1].Value;
            var body = match.Groups[2].Value;

            var fields = ParseSdlFields(body);
            types.Add(new SdlType { Name = typeName, Fields = fields });
        }

        return types;
    }

    private List<SdlField> ParseSdlFields(string body)
    {
        var fields = new List<SdlField>();
        var fieldMatches = FieldDefinitionRegex().Matches(body);

        foreach (Match match in fieldMatches)
        {
            var name = match.Groups[1].Value;
            var args = match.Groups[2].Value;
            var returnType = match.Groups[3].Value;

            fields.Add(new SdlField
            {
                Name = name,
                Arguments = ParseSdlArguments(args),
                ReturnType = returnType.Trim()
            });
        }

        return fields;
    }

    private List<SdlArgument> ParseSdlArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return [];

        var arguments = new List<SdlArgument>();
        var argMatches = ArgumentDefinitionRegex().Matches(args);

        foreach (Match match in argMatches)
        {
            arguments.Add(new SdlArgument
            {
                Name = match.Groups[1].Value,
                Type = match.Groups[2].Value.Trim()
            });
        }

        return arguments;
    }

    private ApiRegistration ConvertSdlToRegistration(List<SdlType> types, string baseUrl, string name)
    {
        var id = GenerateId(name);

        var registration = new ApiRegistration
        {
            Id = id,
            DisplayName = name,
            Description = "GraphQL API (from SDL)",
            BaseUrl = baseUrl,
            OpenApiVersion = "graphql-sdl",
            Auth = new NoAuthConfig(),
            CreatedAt = DateTime.UtcNow,
            LastRefreshed = DateTime.UtcNow,
            Endpoints = []
        };

        // Find Query and Mutation types
        var queryType = types.FirstOrDefault(t => t.Name == "Query");
        var mutationType = types.FirstOrDefault(t => t.Name == "Mutation");

        if (queryType != null)
        {
            foreach (var field in queryType.Fields)
            {
                registration.Endpoints.Add(CreateSdlEndpoint(field, "Query", "query"));
            }
        }

        if (mutationType != null)
        {
            foreach (var field in mutationType.Fields)
            {
                registration.Endpoints.Add(CreateSdlEndpoint(field, "Mutation", "mutation"));
            }
        }

        return registration;
    }

    private static ApiEndpoint CreateSdlEndpoint(SdlField field, string tag, string prefix)
    {
        var endpoint = new ApiEndpoint
        {
            Id = $"{prefix}-{field.Name}",
            OperationId = $"{prefix}-{field.Name}",
            Method = "POST",
            Path = $"/graphql#{prefix}.{field.Name}",
            Summary = field.Name,
            Description = $"{tag}: {field.Name} -> {field.ReturnType}",
            Tags = [tag],
            Parameters = [],
            Responses = new Dictionary<string, ResponseDefinition>
            {
                ["200"] = new() { Description = field.ReturnType }
            }
        };

        foreach (var arg in field.Arguments)
        {
            endpoint.Parameters.Add(new ParameterDefinition
            {
                Name = arg.Name,
                In = "body",
                Required = arg.Type.EndsWith('!'),
                Schema = ConvertSdlTypeToJsonSchema(arg.Type)
            });
        }

        return endpoint;
    }

    private static JsonSchema ConvertSdlTypeToJsonSchema(string sdlType)
    {
        var type = sdlType.TrimEnd('!');
        var isList = type.StartsWith('[') && type.EndsWith(']');

        if (isList)
        {
            var innerType = type[1..^1].TrimEnd('!');
            return new JsonSchema
            {
                Type = "array",
                Items = ConvertScalarType(innerType)
            };
        }

        return ConvertScalarType(type);
    }

    private static JsonSchema ConvertScalarType(string type)
    {
        return type.ToUpperInvariant() switch
        {
            "STRING" or "ID" => new JsonSchema { Type = "string" },
            "INT" => new JsonSchema { Type = "integer" },
            "FLOAT" => new JsonSchema { Type = "number" },
            "BOOLEAN" => new JsonSchema { Type = "boolean" },
            _ => new JsonSchema { Type = "object", Description = type }
        };
    }

    private static string GenerateId(string input)
    {
        var slug = Regex.Replace(input.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        // Extract domain-like part if it's a URL
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            slug = Regex.Replace(uri.Host.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        }
        return string.IsNullOrEmpty(slug) ? "graphql-api" : $"{slug}-graphql";
    }

    private static string ExtractNameFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.Replace("www.", "");
            var parts = host.Split('.');
            if (parts.Length >= 2)
            {
                return $"{char.ToUpper(parts[0][0])}{parts[0][1..]} GraphQL API";
            }
            return $"{host} GraphQL API";
        }
        return "GraphQL API";
    }

    [GeneratedRegex(@"type\s+(\w+)\s*\{([^}]+)\}", RegexOptions.Singleline)]
    private static partial Regex TypeDefinitionRegex();

    [GeneratedRegex(@"(\w+)\s*(?:\(([^)]*)\))?\s*:\s*([^\n]+)")]
    private static partial Regex FieldDefinitionRegex();

    [GeneratedRegex(@"(\w+)\s*:\s*([^,\)]+)")]
    private static partial Regex ArgumentDefinitionRegex();
}

// Response models for introspection
internal class GraphQLResponse<T>
{
    public T? Data { get; set; }
    public List<GraphQLError>? Errors { get; set; }
}

internal class GraphQLError
{
    public string Message { get; set; } = "";
}

internal class IntrospectionData
{
    public IntrospectionResult? Schema { get; set; }
    public IntrospectionResult? __schema { get => Schema; set => Schema = value; }
}

internal class IntrospectionResult
{
    public GraphQLTypeName? QueryType { get; set; }
    public GraphQLTypeName? MutationType { get; set; }
    public GraphQLTypeName? SubscriptionType { get; set; }
    public List<GraphQLType>? Types { get; set; }
}

internal class GraphQLTypeName
{
    public string? Name { get; set; }
}

internal class GraphQLType
{
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<GraphQLField>? Fields { get; set; }
    public List<GraphQLInputValue>? InputFields { get; set; }
    public List<GraphQLEnumValue>? EnumValues { get; set; }
}

internal class GraphQLField
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<GraphQLInputValue>? Args { get; set; }
    public GraphQLTypeRef? Type { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}

internal class GraphQLInputValue
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public GraphQLTypeRef? Type { get; set; }
    public string? DefaultValue { get; set; }
}

internal class GraphQLTypeRef
{
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public GraphQLTypeRef? OfType { get; set; }
}

internal class GraphQLEnumValue
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}

// SDL parsing models
internal class SdlType
{
    public string Name { get; set; } = "";
    public List<SdlField> Fields { get; set; } = [];
}

internal class SdlField
{
    public string Name { get; set; } = "";
    public List<SdlArgument> Arguments { get; set; } = [];
    public string ReturnType { get; set; } = "";
}

internal class SdlArgument
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}
