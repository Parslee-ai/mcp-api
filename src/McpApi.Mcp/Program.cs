using McpApi.Core.Auth;
using McpApi.Core.Http;
using McpApi.Core.Secrets;
using McpApi.Core.Services;
using McpApi.Core.Storage;
using McpApi.Mcp;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Configure Cosmos DB
var cosmosConnectionString = builder.Configuration["Cosmos:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("MCPAPI_COSMOS_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Cosmos connection string not configured. Set Cosmos:ConnectionString or MCPAPI_COSMOS_CONNECTION_STRING.");

var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "mcpapi";

builder.Services.AddSingleton<IApiRegistrationStore>(sp =>
    new CosmosApiRegistrationStore(cosmosConnectionString, databaseName));

// Configure encryption service
var masterKey = builder.Configuration["Encryption:MasterKey"]
    ?? Environment.GetEnvironmentVariable("MCPAPI_MASTER_KEY")
    ?? throw new InvalidOperationException(
        "Encryption master key not configured. Set Encryption:MasterKey or MCPAPI_MASTER_KEY environment variable.");

builder.Services.AddSingleton<IEncryptionService>(new AesGcmEncryptionService(masterKey));

// Configure Key Vault (optional - for legacy secrets)
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
ISecretProvider? keyVaultProvider = null;
if (!string.IsNullOrEmpty(keyVaultUri))
{
    keyVaultProvider = new KeyVaultSecretProvider(new SecretProviderOptions { VaultUri = keyVaultUri });
    builder.Services.AddSingleton(keyVaultProvider);
}

// Configure secret resolver (handles both encrypted and Key Vault secrets)
builder.Services.AddSingleton<ISecretResolver>(sp =>
{
    var encryptionService = sp.GetRequiredService<IEncryptionService>();
    var kvProvider = sp.GetService<ISecretProvider>(); // Optional
    return new SecretResolver(encryptionService, kvProvider);
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthHandlerFactory>(sp =>
{
    var secretResolver = sp.GetRequiredService<ISecretResolver>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new AuthHandlerFactory(secretResolver, httpClientFactory);
});
builder.Services.AddSingleton<IApiClient, DynamicApiClient>();

// Configure Cosmos client for usage tracking
builder.Services.AddSingleton(_ =>
{
    var options = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };
    return new CosmosClient(cosmosConnectionString, options);
});

// Configure usage tracking
builder.Services.AddSingleton<IUsageStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosUsageStore(cosmosClient, databaseName);
});

builder.Services.AddSingleton<IUsageTrackingService>(sp =>
{
    var usageStore = sp.GetRequiredService<IUsageStore>();
    var apiStore = sp.GetRequiredService<IApiRegistrationStore>();
    return new UsageTrackingService(usageStore, apiStore);
});

// Configure user store for token validation
builder.Services.AddSingleton<IUserStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosUserStore(cosmosClient, databaseName);
});

// Configure MCP token service
builder.Services.AddSingleton<IMcpTokenStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosMcpTokenStore(cosmosClient, databaseName);
});

builder.Services.AddSingleton<IMcpTokenService>(sp =>
{
    var tokenStore = sp.GetRequiredService<IMcpTokenStore>();
    return new McpTokenService(tokenStore);
});

// User context for multi-tenancy with token-based authentication
// Uses MCPAPI_TOKEN if set, otherwise falls back to MCPAPI_USER_ID for development
builder.Services.AddSingleton<IMcpCurrentUser>(sp =>
{
    var token = Environment.GetEnvironmentVariable("MCPAPI_TOKEN");
    if (!string.IsNullOrEmpty(token))
    {
        // Production: Use token-based authentication
        var tokenService = sp.GetRequiredService<IMcpTokenService>();
        var userStore = sp.GetRequiredService<IUserStore>();
        return new TokenMcpCurrentUser(tokenService, userStore);
    }
    else
    {
        // Development fallback: Use environment variables
        #pragma warning disable CS0618 // Obsolete warning suppressed for development mode
        return new EnvironmentMcpCurrentUser();
        #pragma warning restore CS0618
    }
});

builder.Services.AddScoped<DynamicToolProvider>();

// Configure MCP Server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "McpApi",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<DynamicToolProvider>();

await builder.Build().RunAsync();
