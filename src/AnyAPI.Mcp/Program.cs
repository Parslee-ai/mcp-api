using AnyAPI.Core.Auth;
using AnyAPI.Core.Http;
using AnyAPI.Core.OpenApi;
using AnyAPI.Core.Secrets;
using AnyAPI.Core.Storage;
using AnyAPI.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddSingleton<SecretProviderOptions>(sp =>
    new SecretProviderOptions
    {
        VaultUri = builder.Configuration["KeyVault:VaultUri"]
            ?? throw new InvalidOperationException("KeyVault:VaultUri not configured")
    });

builder.Services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();

builder.Services.AddSingleton<IApiRegistrationStore>(sp =>
    new CosmosApiRegistrationStore(
        builder.Configuration["Cosmos:ConnectionString"]
            ?? throw new InvalidOperationException("Cosmos:ConnectionString not configured"),
        builder.Configuration["Cosmos:DatabaseName"] ?? "anyapi"));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthHandlerFactory, AuthHandlerFactory>();
builder.Services.AddSingleton<IApiClient, DynamicApiClient>();
builder.Services.AddSingleton<DynamicToolProvider>();

// Configure MCP Server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "AnyAPI",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<DynamicToolProvider>();

await builder.Build().RunAsync();
