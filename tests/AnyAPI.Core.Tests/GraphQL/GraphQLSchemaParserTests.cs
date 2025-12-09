namespace AnyAPI.Core.Tests.GraphQL;

using AnyAPI.Core.GraphQL;
using Xunit;

public class GraphQLSchemaParserTests
{
    [Fact]
    public void IsGraphQLSchema_WithTypeQuery_ReturnsTrue()
    {
        // Arrange
        var sdl = """
            type Query {
                users: [User]
                user(id: ID!): User
            }

            type User {
                id: ID!
                name: String
            }
            """;

        // Act
        var result = GraphQLSchemaParser.IsGraphQLSchema(sdl);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGraphQLSchema_WithTypeMutation_ReturnsTrue()
    {
        // Arrange
        var sdl = """
            type Mutation {
                createUser(name: String!): User
            }
            """;

        // Act
        var result = GraphQLSchemaParser.IsGraphQLSchema(sdl);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGraphQLSchema_WithSchemaBlock_ReturnsTrue()
    {
        // Arrange
        var sdl = """
            schema {
                query: Query
                mutation: Mutation
            }
            """;

        // Act
        var result = GraphQLSchemaParser.IsGraphQLSchema(sdl);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGraphQLSchema_WithOpenApiSpec_ReturnsFalse()
    {
        // Arrange
        var json = """
            {
                "openapi": "3.0.0",
                "info": { "title": "Test API", "version": "1.0" },
                "paths": {}
            }
            """;

        // Act
        var result = GraphQLSchemaParser.IsGraphQLSchema(json);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsGraphQLSchema_WithPostmanCollection_ReturnsFalse()
    {
        // Arrange
        var json = """
            {
                "info": {
                    "name": "Test API",
                    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
                },
                "item": []
            }
            """;

        // Act
        var result = GraphQLSchemaParser.IsGraphQLSchema(json);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("https://api.example.com/graphql", true)]
    [InlineData("https://api.example.com/v1/graphql", true)]
    [InlineData("https://example.com/gql", true)]
    [InlineData("https://api.example.com/api", false)]
    [InlineData("https://api.example.com/openapi.json", false)]
    public void LooksLikeGraphQLEndpoint_ReturnsExpected(string url, bool expected)
    {
        // Act
        var result = GraphQLSchemaParser.LooksLikeGraphQLEndpoint(url);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseFromSdl_WithSimpleSchema_ExtractsQueries()
    {
        // Arrange
        var sdl = """
            type Query {
                users: [User]
                user(id: ID!): User
            }

            type User {
                id: ID!
                name: String
                email: String
            }
            """;

        using var httpClient = new HttpClient();
        var parser = new GraphQLSchemaParser(httpClient);

        // Act
        var registration = parser.ParseFromSdl(sdl, "https://api.example.com/graphql", "Test API");

        // Assert
        Assert.NotNull(registration);
        Assert.Equal("test-api-graphql", registration.Id);
        Assert.Equal("Test API", registration.DisplayName);
        Assert.Equal("https://api.example.com/graphql", registration.BaseUrl);
        Assert.Equal("graphql-sdl", registration.OpenApiVersion);
        Assert.Equal(2, registration.Endpoints.Count);

        var usersQuery = registration.Endpoints.FirstOrDefault(e => e.Id == "query-users");
        Assert.NotNull(usersQuery);
        Assert.Equal("POST", usersQuery.Method);
        Assert.Contains("Query", usersQuery.Tags);

        var userQuery = registration.Endpoints.FirstOrDefault(e => e.Id == "query-user");
        Assert.NotNull(userQuery);
        Assert.Single(userQuery.Parameters);
        Assert.Equal("id", userQuery.Parameters[0].Name);
        Assert.True(userQuery.Parameters[0].Required); // ID! is required
    }

    [Fact]
    public void ParseFromSdl_WithMutations_ExtractsMutations()
    {
        // Arrange
        var sdl = """
            type Query {
                user(id: ID!): User
            }

            type Mutation {
                createUser(name: String!, email: String): User
                deleteUser(id: ID!): Boolean
            }

            type User {
                id: ID!
                name: String
            }
            """;

        using var httpClient = new HttpClient();
        var parser = new GraphQLSchemaParser(httpClient);

        // Act
        var registration = parser.ParseFromSdl(sdl, "https://api.example.com/graphql");

        // Assert
        Assert.Equal(3, registration.Endpoints.Count);

        var createMutation = registration.Endpoints.FirstOrDefault(e => e.Id == "mutation-createUser");
        Assert.NotNull(createMutation);
        Assert.Contains("Mutation", createMutation.Tags);
        Assert.Equal(2, createMutation.Parameters.Count);

        var deleteMutation = registration.Endpoints.FirstOrDefault(e => e.Id == "mutation-deleteUser");
        Assert.NotNull(deleteMutation);
    }

    [Fact]
    public void ParseFromSdl_WithArrayTypes_SetsSchemaCorrectly()
    {
        // Arrange
        var sdl = """
            type Query {
                users: [User]
            }

            type User {
                id: ID!
            }
            """;

        using var httpClient = new HttpClient();
        var parser = new GraphQLSchemaParser(httpClient);

        // Act
        var registration = parser.ParseFromSdl(sdl, "https://api.example.com/graphql");

        // Assert
        var endpoint = registration.Endpoints.Single();
        Assert.Contains("[User]", endpoint.Responses["200"].Description);
    }

    [Fact]
    public void ParseFromSdl_WithScalarArgs_InfersJsonSchemaTypes()
    {
        // Arrange
        var sdl = """
            type Query {
                search(query: String, limit: Int, active: Boolean): [Result]
            }

            type Result {
                id: ID!
            }
            """;

        using var httpClient = new HttpClient();
        var parser = new GraphQLSchemaParser(httpClient);

        // Act
        var registration = parser.ParseFromSdl(sdl, "https://api.example.com/graphql");

        // Assert
        var endpoint = registration.Endpoints.Single();
        Assert.Equal(3, endpoint.Parameters.Count);

        var queryParam = endpoint.Parameters.First(p => p.Name == "query");
        Assert.Equal("string", queryParam.Schema?.Type);

        var limitParam = endpoint.Parameters.First(p => p.Name == "limit");
        Assert.Equal("integer", limitParam.Schema?.Type);

        var activeParam = endpoint.Parameters.First(p => p.Name == "active");
        Assert.Equal("boolean", activeParam.Schema?.Type);
    }

    [Fact]
    public void ParseFromSdl_WithRequiredArgs_SetsRequiredFlag()
    {
        // Arrange
        var sdl = """
            type Query {
                user(id: ID!, name: String): User
            }

            type User {
                id: ID!
            }
            """;

        using var httpClient = new HttpClient();
        var parser = new GraphQLSchemaParser(httpClient);

        // Act
        var registration = parser.ParseFromSdl(sdl, "https://api.example.com/graphql");

        // Assert
        var endpoint = registration.Endpoints.Single();

        var idParam = endpoint.Parameters.First(p => p.Name == "id");
        Assert.True(idParam.Required);

        var nameParam = endpoint.Parameters.First(p => p.Name == "name");
        Assert.False(nameParam.Required);
    }
}
