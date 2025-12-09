namespace AnyAPI.Core.Storage;

using Microsoft.Azure.Cosmos;

/// <summary>
/// Factory for initializing Cosmos DB containers.
/// </summary>
public static class CosmosContainerFactory
{
    /// <summary>
    /// Ensures the database and container exist with proper configuration.
    /// </summary>
    public static async Task<Container> CreateContainerIfNotExistsAsync(
        string connectionString,
        string databaseName,
        CancellationToken ct = default)
    {
        var client = new CosmosClient(connectionString, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });

        // Create database if not exists
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(
            databaseName,
            cancellationToken: ct);

        // Define container properties
        var containerProperties = new ContainerProperties
        {
            Id = "api-registrations",
            PartitionKeyPath = "/id",
            IndexingPolicy = new IndexingPolicy
            {
                Automatic = true,
                IndexingMode = IndexingMode.Consistent,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/endpoints/*" },  // Large array, exclude from indexing
                    new ExcludedPath { Path = "/_etag/?" }
                }
            }
        };

        // Create container if not exists
        var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
            containerProperties,
            cancellationToken: ct);

        return containerResponse.Container;
    }
}
