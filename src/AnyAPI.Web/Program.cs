using AnyAPI.Core.Auth;
using AnyAPI.Core.GraphQL;
using AnyAPI.Core.Http;
using AnyAPI.Core.OpenApi;
using AnyAPI.Core.Postman;
using AnyAPI.Core.Secrets;
using AnyAPI.Core.Storage;
using AnyAPI.Web.Components;
using AnyAPI.Web.Services;
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

// Configure typed HTTP clients
builder.Services.AddHttpClient<OpenApiParser>();
builder.Services.AddHttpClient<OpenApiDiscovery>();
builder.Services.AddHttpClient<PostmanCollectionParser>();
builder.Services.AddHttpClient<GraphQLSchemaParser>();
builder.Services.AddHttpClient<DynamicApiClient>();

// Register services - typed HttpClient services are transient by default
builder.Services.AddTransient<IOpenApiParser>(sp => sp.GetRequiredService<OpenApiParser>());
builder.Services.AddTransient<IApiClient>(sp => sp.GetRequiredService<DynamicApiClient>());
builder.Services.AddSingleton<IAuthHandlerFactory, AuthHandlerFactory>();
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
