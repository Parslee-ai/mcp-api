namespace AnyAPI.Core.Storage;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Custom Cosmos DB serializer using System.Text.Json.
/// Required for proper polymorphic type support (AuthConfiguration).
/// </summary>
public class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public CosmosSystemTextJsonSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
    }

    public override T FromStream<T>(Stream stream)
    {
        if (stream.CanSeek && stream.Length == 0)
        {
            return default!;
        }

        using (stream)
        {
            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, _options);
        stream.Position = 0;
        return stream;
    }
}
