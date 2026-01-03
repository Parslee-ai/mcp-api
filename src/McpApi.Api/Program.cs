using System.Text;
using McpApi.Api.Auth;
using McpApi.Api.Services;
using McpApi.Core.Auth;
using McpApi.Core.GraphQL;
using McpApi.Core.Http;
using McpApi.Core.Notifications;
using McpApi.Core.OpenApi;
using McpApi.Core.Postman;
using McpApi.Core.Secrets;
using McpApi.Core.Services;
using McpApi.Core.Storage;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MCP-API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT authentication
var jwtSecretName = builder.Configuration["Jwt:SecretName"] ?? "jwt-signing-key";
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? builder.Configuration["jwt-secret"]
    ?? builder.Configuration[jwtSecretName]
    ?? throw new InvalidOperationException("JWT secret not configured");

builder.Services.Configure<JwtOptions>(options =>
{
    options.Secret = jwtSecret;
    options.Issuer = builder.Configuration["Jwt:Issuer"] ?? "McpApi";
    options.Audience = builder.Configuration["Jwt:Audience"] ?? "McpApi";
    options.AccessTokenExpirationMinutes = int.Parse(builder.Configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");
    options.RefreshTokenExpirationDays = int.Parse(builder.Configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "McpApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "McpApi",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure Cosmos DB
var connectionString = builder.Configuration["Cosmos:ConnectionString"]
    ?? builder.Configuration["cosmos-connection-string"]
    ?? throw new InvalidOperationException("Cosmos connection string not configured");
var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "mcpapi";

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

// Configure encryption service
var masterKeySecretName = builder.Configuration["Encryption:MasterKeySecretName"] ?? "mcpapi-master-encryption-key";
var masterKey = builder.Configuration[masterKeySecretName]
    ?? builder.Configuration["Encryption:MasterKey"];

if (string.IsNullOrEmpty(masterKey))
{
    Console.WriteLine("[WARNING] No encryption master key configured. Using development key - DO NOT USE IN PRODUCTION.");
    masterKey = Convert.ToBase64String(new byte[32]);
}

builder.Services.AddSingleton<IEncryptionService>(new AesGcmEncryptionService(masterKey));

// Configure secret resolver
ISecretProvider? keyVaultProvider = null;
if (!string.IsNullOrEmpty(keyVaultUri))
{
    keyVaultProvider = new KeyVaultSecretProvider(new SecretProviderOptions { VaultUri = keyVaultUri });
    builder.Services.AddSingleton(keyVaultProvider);
}

builder.Services.AddSingleton<ISecretResolver>(sp =>
{
    var encryptionService = sp.GetRequiredService<IEncryptionService>();
    var kvProvider = sp.GetService<ISecretProvider>();
    return new SecretResolver(encryptionService, kvProvider);
});

// Configure storage
builder.Services.AddSingleton<IApiRegistrationStore>(sp =>
    new CosmosApiRegistrationStore(connectionString!, databaseName));

builder.Services.AddSingleton<IUserStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosUserStore(cosmosClient, databaseName);
});

builder.Services.AddSingleton<IUsageStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosUsageStore(cosmosClient, databaseName);
});

builder.Services.AddSingleton<IMcpTokenStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosMcpTokenStore(cosmosClient, databaseName);
});

builder.Services.AddSingleton<IRefreshTokenStore>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    return new CosmosRefreshTokenStore(cosmosClient, databaseName);
});

// Configure services
builder.Services.AddSingleton<IUsageTrackingService>(sp =>
{
    var usageStore = sp.GetRequiredService<IUsageStore>();
    var apiStore = sp.GetRequiredService<IApiRegistrationStore>();
    return new UsageTrackingService(usageStore, apiStore);
});

builder.Services.AddSingleton<IMcpTokenService>(sp =>
{
    var tokenStore = sp.GetRequiredService<IMcpTokenStore>();
    return new McpTokenService(tokenStore);
});

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// Configure notification services (no-op for API)
builder.Services.AddSingleton<IEmailService>(_ => new NoOpEmailService());
builder.Services.AddSingleton<ISmsService>(_ => new NoOpSmsService());

// Configure auth service
var baseUrl = builder.Configuration["App:BaseUrl"] ?? "https://api.mcp-api.ai";
builder.Services.AddScoped<IAuthService>(sp =>
{
    var userStore = sp.GetRequiredService<IUserStore>();
    var emailService = sp.GetRequiredService<IEmailService>();
    var smsService = sp.GetRequiredService<ISmsService>();
    return new AuthService(userStore, emailService, smsService, baseUrl);
});

// Configure current user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

// Configure HTTP clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<OpenApiParser>();
builder.Services.AddHttpClient<OpenApiDiscovery>();
builder.Services.AddHttpClient<PostmanCollectionParser>();
builder.Services.AddHttpClient<GraphQLSchemaParser>();

// Register parsers
builder.Services.AddTransient<IOpenApiParser>(sp => sp.GetRequiredService<OpenApiParser>());
builder.Services.AddSingleton<IAuthHandlerFactory>(sp =>
{
    var secretResolver = sp.GetRequiredService<ISecretResolver>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new AuthHandlerFactory(secretResolver, httpClientFactory);
});
builder.Services.AddSingleton<IApiClient, DynamicApiClient>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

// Only use HTTPS redirection in development (Container Apps handles SSL termination)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// No-op implementations for API (email verification handled by Web frontend)
internal class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string plainTextContent, string htmlContent, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[EMAIL] To: {to}, Subject: {subject}");
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
