using Microsoft.Azure.Cosmos;
using User = McpApi.Core.Models.User;

namespace McpApi.Core.Storage;

/// <summary>
/// Cosmos DB implementation of user storage.
/// </summary>
public class CosmosUserStore : IUserStore
{
    private readonly Container _container;
    private const string ContainerName = "users";

    public CosmosUserStore(CosmosClient cosmosClient, string databaseName)
    {
        var database = cosmosClient.GetDatabase(databaseName);
        _container = database.GetContainer(ContainerName);
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<User>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Email is stored as lowercase (normalized at write time in AuthService)
        // Direct equality check is more efficient than LOWER() which prevents index usage
        var normalizedEmail = email.ToLowerInvariant();
        var query = new QueryDefinition("SELECT * FROM c WHERE c.email = @email")
            .WithParameter("@email", normalizedEmail);

        using var iterator = _container.GetItemQueryIterator<User>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task<User?> GetByOAuthProviderAsync(string provider, string providerId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.oAuthProvider = @provider AND c.oAuthProviderId = @providerId")
            .WithParameter("@provider", provider)
            .WithParameter("@providerId", providerId);

        using var iterator = _container.GetItemQueryIterator<User>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.emailVerificationToken = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<User>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task<User> UpsertAsync(User user, CancellationToken cancellationToken = default)
    {
        var response = await _container.UpsertItemAsync(
            user,
            new PartitionKey(user.Id),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<User>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted, ignore
        }
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await GetByEmailAsync(email, cancellationToken);
        return user != null;
    }
}
