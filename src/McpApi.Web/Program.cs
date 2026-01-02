using McpApi.Core.Auth;
using McpApi.Core.GraphQL;
using McpApi.Core.Http;
using McpApi.Core.Notifications;
using McpApi.Core.OpenApi;
using McpApi.Core.Postman;
using McpApi.Core.Secrets;
using McpApi.Core.Storage;
using McpApi.Web.Components;
using McpApi.Web.Services;
using Azure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Azure.Cosmos;

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

// Add authentication services
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthenticationStateProvider>());

// Configure Cosmos DB
var connectionString = builder.Configuration["Cosmos:ConnectionString"];
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration["cosmos-connection-string"];
}
var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "mcpapi";

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Cosmos connection string not configured. Set Cosmos:ConnectionString or store as cosmos-connection-string in Key Vault.");
}

// Register Cosmos client as singleton
builder.Services.AddSingleton(_ =>
{
    var options = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };
    return new CosmosClient(connectionString, options);
});

// Configure MCP-API services
builder.Services.AddSingleton<SecretProviderOptions>(sp =>
    new SecretProviderOptions
    {
        VaultUri = keyVaultUri ?? ""
    });

builder.Services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();

builder.Services.AddSingleton<IApiRegistrationStore>(sp =>
    new CosmosApiRegistrationStore(connectionString!, databaseName));

builder.Services.AddSingleton<IUserStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosUserStore(cosmosClient, databaseName);
});

// Configure notification services
var acsConnectionString = builder.Configuration["ACS:ConnectionString"];
var acsEmailSender = builder.Configuration["ACS:EmailSender"] ?? "noreply@mcp-api.ai";
var acsSmsSender = builder.Configuration["ACS:SmsSender"] ?? "";

builder.Services.AddSingleton<IEmailService>(sp =>
{
    if (!string.IsNullOrEmpty(acsConnectionString))
    {
        return new AcsEmailService(acsConnectionString, acsEmailSender);
    }
    // Return a no-op implementation for development
    return new NoOpEmailService();
});

builder.Services.AddSingleton<ISmsService>(sp =>
{
    if (!string.IsNullOrEmpty(acsConnectionString) && !string.IsNullOrEmpty(acsSmsSender))
    {
        return new AcsSmsService(acsConnectionString, acsSmsSender);
    }
    // Return a no-op implementation for development
    return new NoOpSmsService();
});

// Configure auth service
var baseUrl = builder.Configuration["App:BaseUrl"] ?? "https://localhost:5001";
builder.Services.AddScoped<IAuthService>(sp =>
{
    var userStore = sp.GetRequiredService<IUserStore>();
    var emailService = sp.GetRequiredService<IEmailService>();
    var smsService = sp.GetRequiredService<ISmsService>();
    return new AuthService(userStore, emailService, smsService, baseUrl);
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

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

// No-op implementations for development without ACS
internal class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string plainTextContent, string htmlContent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[EMAIL] To: {to}, Subject: {subject}");
        Console.WriteLine($"[EMAIL] Content: {plainTextContent}");
        return Task.CompletedTask;
    }
}

internal class NoOpSmsService : ISmsService
{
    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SMS] To: {phoneNumber}, Message: {message}");
        return Task.CompletedTask;
    }
}
