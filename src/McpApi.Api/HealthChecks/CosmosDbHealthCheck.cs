using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace McpApi.Api.HealthChecks;

/// <summary>
/// Health check that verifies Cosmos DB connectivity.
/// </summary>
public class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;

    public CosmosDbHealthCheck(CosmosClient cosmosClient, string databaseName)
    {
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Attempt to read the database to verify connectivity
            var database = _cosmosClient.GetDatabase(_databaseName);
            _ = await database.ReadAsync(cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy($"Cosmos DB database '{_databaseName}' is accessible");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return HealthCheckResult.Unhealthy($"Cosmos DB database '{_databaseName}' not found", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to connect to Cosmos DB", ex);
        }
    }
}

/// <summary>
/// Extension methods for adding Cosmos DB health check.
/// </summary>
public static class CosmosDbHealthCheckExtensions
{
    public static IHealthChecksBuilder AddCosmosDb(
        this IHealthChecksBuilder builder,
        CosmosClient cosmosClient,
        string databaseName,
        string name = "cosmosdb",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new CosmosDbHealthCheck(cosmosClient, databaseName),
            failureStatus,
            tags));
    }
}
