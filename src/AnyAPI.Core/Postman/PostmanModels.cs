namespace AnyAPI.Core.Postman;

using System.Text.Json.Serialization;

/// <summary>
/// Postman Collection v2.1 format models.
/// See: https://schema.postman.com/collection/json/v2.1.0/draft-07/collection.json
/// </summary>
public class PostmanCollection
{
    [JsonPropertyName("info")]
    public PostmanInfo Info { get; set; } = new();

    [JsonPropertyName("item")]
    public List<PostmanItem> Item { get; set; } = [];

    [JsonPropertyName("variable")]
    public List<PostmanVariable>? Variable { get; set; }

    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

public class PostmanInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("_postman_id")]
    public string? PostmanId { get; set; }
}

/// <summary>
/// Can be either a folder (with nested items) or a request.
/// </summary>
public class PostmanItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Nested items (if this is a folder).</summary>
    [JsonPropertyName("item")]
    public List<PostmanItem>? Item { get; set; }

    /// <summary>Request definition (if this is an endpoint).</summary>
    [JsonPropertyName("request")]
    public PostmanRequest? Request { get; set; }

    [JsonPropertyName("response")]
    public List<PostmanResponse>? Response { get; set; }
}

public class PostmanRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("header")]
    public List<PostmanHeader>? Header { get; set; }

    [JsonPropertyName("body")]
    public PostmanBody? Body { get; set; }

    [JsonPropertyName("url")]
    public PostmanUrl? Url { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("auth")]
    public PostmanAuth? Auth { get; set; }
}

public class PostmanUrl
{
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }

    [JsonPropertyName("host")]
    public List<string>? Host { get; set; }

    [JsonPropertyName("path")]
    public List<string>? Path { get; set; }

    [JsonPropertyName("query")]
    public List<PostmanQueryParam>? Query { get; set; }

    [JsonPropertyName("variable")]
    public List<PostmanVariable>? Variable { get; set; }
}

public class PostmanQueryParam
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

public class PostmanVariable
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class PostmanHeader
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

public class PostmanBody
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("options")]
    public PostmanBodyOptions? Options { get; set; }

    [JsonPropertyName("formdata")]
    public List<PostmanFormData>? FormData { get; set; }

    [JsonPropertyName("urlencoded")]
    public List<PostmanFormData>? UrlEncoded { get; set; }
}

public class PostmanBodyOptions
{
    [JsonPropertyName("raw")]
    public PostmanRawOptions? Raw { get; set; }
}

public class PostmanRawOptions
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

public class PostmanFormData
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; set; }
}

public class PostmanAuth
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "noauth";

    [JsonPropertyName("bearer")]
    public List<PostmanAuthParam>? Bearer { get; set; }

    [JsonPropertyName("apikey")]
    public List<PostmanAuthParam>? ApiKey { get; set; }

    [JsonPropertyName("basic")]
    public List<PostmanAuthParam>? Basic { get; set; }

    [JsonPropertyName("oauth2")]
    public List<PostmanAuthParam>? OAuth2 { get; set; }
}

public class PostmanAuthParam
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class PostmanResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }
}
