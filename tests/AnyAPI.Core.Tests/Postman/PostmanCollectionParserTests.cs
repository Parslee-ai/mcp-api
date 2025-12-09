namespace AnyAPI.Core.Tests.Postman;

using AnyAPI.Core.Postman;
using Xunit;

public class PostmanCollectionParserTests
{
    [Fact]
    public void IsPostmanCollection_WithValidCollection_ReturnsTrue()
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
        var result = PostmanCollectionParser.IsPostmanCollection(json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPostmanCollection_WithPostmanId_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Test API",
                "_postman_id": "abc-123-def"
            },
            "item": []
        }
        """;

        // Act
        var result = PostmanCollectionParser.IsPostmanCollection(json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPostmanCollection_WithRequestItems_ReturnsTrue()
    {
        // Arrange
        var json = """
        {
            "info": { "name": "Test" },
            "item": [
                {
                    "name": "Get Users",
                    "request": {
                        "method": "GET",
                        "url": "https://api.example.com/users"
                    }
                }
            ]
        }
        """;

        // Act
        var result = PostmanCollectionParser.IsPostmanCollection(json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPostmanCollection_WithOpenApiSpec_ReturnsFalse()
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
        var result = PostmanCollectionParser.IsPostmanCollection(json);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPostmanCollection_WithInvalidJson_ReturnsFalse()
    {
        // Arrange
        var json = "not valid json";

        // Act
        var result = PostmanCollectionParser.IsPostmanCollection(json);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ParseFromJson_WithSimpleCollection_ExtractsEndpoints()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Test API Collection",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "item": [
                {
                    "name": "Get Users",
                    "request": {
                        "method": "GET",
                        "url": {
                            "raw": "https://api.example.com/users",
                            "protocol": "https",
                            "host": ["api", "example", "com"],
                            "path": ["users"]
                        }
                    }
                },
                {
                    "name": "Create User",
                    "request": {
                        "method": "POST",
                        "url": {
                            "raw": "https://api.example.com/users",
                            "protocol": "https",
                            "host": ["api", "example", "com"],
                            "path": ["users"]
                        },
                        "body": {
                            "mode": "raw",
                            "raw": "{\"name\": \"John\", \"email\": \"john@example.com\"}"
                        }
                    }
                }
            ]
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        Assert.NotNull(registration);
        Assert.Equal("test-api-collection", registration.Id);
        Assert.Equal("Test API Collection", registration.DisplayName);
        Assert.Equal("https://api.example.com", registration.BaseUrl);
        Assert.Equal(2, registration.Endpoints.Count);

        var getEndpoint = registration.Endpoints.First(e => e.Method == "GET");
        Assert.Equal("/users", getEndpoint.Path);
        Assert.Equal("Get Users", getEndpoint.Summary);

        var postEndpoint = registration.Endpoints.First(e => e.Method == "POST");
        Assert.Equal("/users", postEndpoint.Path);
        Assert.NotNull(postEndpoint.RequestBody);
    }

    [Fact]
    public void ParseFromJson_WithNestedFolders_FlattensEndpoints()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Nested API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "item": [
                {
                    "name": "Users",
                    "item": [
                        {
                            "name": "Get All Users",
                            "request": {
                                "method": "GET",
                                "url": {
                                    "raw": "https://api.example.com/users",
                                    "path": ["users"]
                                }
                            }
                        },
                        {
                            "name": "Admin",
                            "item": [
                                {
                                    "name": "Get Admin Users",
                                    "request": {
                                        "method": "GET",
                                        "url": {
                                            "raw": "https://api.example.com/users/admin",
                                            "path": ["users", "admin"]
                                        }
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        Assert.Equal(2, registration.Endpoints.Count);
        Assert.All(registration.Endpoints, e => Assert.Contains("Users", e.Tags));
    }

    [Fact]
    public void ParseFromJson_WithPathParameters_ExtractsParameters()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Path Params API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "item": [
                {
                    "name": "Get User by ID",
                    "request": {
                        "method": "GET",
                        "url": {
                            "raw": "https://api.example.com/users/:userId",
                            "path": [":userId"],
                            "variable": [
                                {
                                    "key": "userId",
                                    "value": "123",
                                    "description": "The user's unique identifier"
                                }
                            ]
                        }
                    }
                }
            ]
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        var endpoint = registration.Endpoints.Single();
        Assert.Equal("/{userId}", endpoint.Path);
        Assert.Single(endpoint.Parameters);
        var param = endpoint.Parameters.First();
        Assert.Equal("userId", param.Name);
        Assert.Equal("path", param.In);
        Assert.True(param.Required);
    }

    [Fact]
    public void ParseFromJson_WithQueryParameters_ExtractsParameters()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Query Params API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "item": [
                {
                    "name": "Search Users",
                    "request": {
                        "method": "GET",
                        "url": {
                            "raw": "https://api.example.com/users?q=test&limit=10",
                            "path": ["users"],
                            "query": [
                                { "key": "q", "value": "test", "description": "Search query" },
                                { "key": "limit", "value": "10", "description": "Max results" }
                            ]
                        }
                    }
                }
            ]
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        var endpoint = registration.Endpoints.Single();
        var queryParams = endpoint.Parameters.Where(p => p.In == "query").ToList();
        Assert.Equal(2, queryParams.Count);
        Assert.Contains(queryParams, p => p.Name == "q");
        Assert.Contains(queryParams, p => p.Name == "limit");
    }

    [Fact]
    public void ParseFromJson_WithBearerAuth_ConvertsToBearerConfig()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Auth API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "auth": {
                "type": "bearer",
                "bearer": [
                    { "key": "token", "value": "{{token}}" }
                ]
            },
            "item": []
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        Assert.Equal("bearer", registration.Auth.AuthType);
    }

    [Fact]
    public void ParseFromJson_WithApiKeyAuth_ConvertsToApiKeyConfig()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "API Key Auth API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "auth": {
                "type": "apikey",
                "apikey": [
                    { "key": "key", "value": "X-API-Key" },
                    { "key": "in", "value": "header" }
                ]
            },
            "item": []
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        Assert.Equal("apiKey", registration.Auth.AuthType);
    }

    [Fact]
    public void ParseFromJson_WithVariables_SubstitutesInBaseUrl()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Variables API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "variable": [
                { "key": "baseUrl", "value": "https://api.prod.example.com" }
            ],
            "item": []
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        Assert.Equal("https://api.prod.example.com", registration.BaseUrl);
    }

    [Fact]
    public void ParseFromJson_WithFormData_CreatesFormRequestBody()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Form API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "item": [
                {
                    "name": "Upload File",
                    "request": {
                        "method": "POST",
                        "url": { "path": ["upload"] },
                        "body": {
                            "mode": "formdata",
                            "formdata": [
                                { "key": "file", "type": "file", "description": "File to upload" },
                                { "key": "name", "type": "text", "value": "test" }
                            ]
                        }
                    }
                }
            ]
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        var endpoint = registration.Endpoints.Single();
        Assert.NotNull(endpoint.RequestBody);
        Assert.Contains("multipart/form-data", endpoint.RequestBody.Content.Keys);
    }

    [Fact]
    public void ParseFromJson_InfersSchemaFromJsonExample()
    {
        // Arrange
        var json = """
        {
            "info": {
                "name": "Schema Inference API",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
            },
            "item": [
                {
                    "name": "Create User",
                    "request": {
                        "method": "POST",
                        "url": { "path": ["users"] },
                        "body": {
                            "mode": "raw",
                            "raw": "{\"name\": \"John\", \"age\": 30, \"active\": true}"
                        }
                    }
                }
            ]
        }
        """;

        using var httpClient = new HttpClient();
        var parser = new PostmanCollectionParser(httpClient);

        // Act
        var registration = parser.ParseFromJson(json);

        // Assert
        var endpoint = registration.Endpoints.Single();
        Assert.NotNull(endpoint.RequestBody);
        var schema = endpoint.RequestBody.Content["application/json"];
        Assert.Equal("object", schema.Type);
        Assert.NotNull(schema.Properties);
        Assert.Contains("name", schema.Properties.Keys);
        Assert.Contains("age", schema.Properties.Keys);
        Assert.Contains("active", schema.Properties.Keys);
    }
}
