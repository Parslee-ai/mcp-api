using McpApi.Core.Auth;
using McpApi.Core.Http;
using McpApi.Core.Secrets;
using McpApi.Core.Storage;
using McpApi.Mcp;
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

// User context for multi-tenancy (Phase 6 will add token auth)
builder.Services.AddSingleton<IMcpCurrentUser, EnvironmentMcpCurrentUser>();
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
