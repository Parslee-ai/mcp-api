using McpApi.Core.Auth;
using McpApi.Core.GraphQL;
using McpApi.Core.Http;
using McpApi.Core.OpenApi;
using McpApi.Core.Postman;
using McpApi.Core.Secrets;
using McpApi.Core.Storage;
using McpApi.Web.Components;
using McpApi.Web.Services;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure AnyAPI services
builder.Services.AddSingleton<SecretProviderOptions>(sp =>
    new SecretProviderOptions
    {
        VaultUri = keyVaultUri ?? ""
    });

builder.Services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();

builder.Services.AddSingleton<IApiRegistrationStore>(sp =>
{
    // Try config first, then Key Vault secret (stored as "cosmos-connection-string")
    var connectionString = builder.Configuration["Cosmos:ConnectionString"];
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = builder.Configuration["cosmos-connection-string"];
    }
    var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "AnyApiDb";

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Cosmos connection string not configured. Set Cosmos:ConnectionString or store as cosmos-connection-string in Key Vault.");
    }

    return new CosmosApiRegistrationStore(connectionString, databaseName);
});

// Configure HTTP clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<OpenApiParser>();
builder.Services.AddHttpClient<OpenApiDiscovery>();
builder.Services.AddHttpClient<PostmanCollectionParser>();
builder.Services.AddHttpClient<GraphQLSchemaParser>();

// Register services
builder.Services.AddTransient<IOpenApiParser>(sp => sp.GetRequiredService<OpenApiParser>());
builder.Services.AddSingleton<IAuthHandlerFactory, AuthHandlerFactory>();
builder.Services.AddSingleton<IApiClient, DynamicApiClient>();
builder.Services.AddScoped<ApiManagementService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
