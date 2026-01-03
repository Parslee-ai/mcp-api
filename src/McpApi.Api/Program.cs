using System.Text;
using AspNetCoreRateLimit;
using McpApi.Api.Auth;
using McpApi.Api.HealthChecks;
using McpApi.Api.Middleware;
using McpApi.Api.Services;
using McpApi.Core.Auth;
using McpApi.Core.GraphQL;
using McpApi.Core.Http;
using McpApi.Core.OpenApi;
using McpApi.Core.Postman;
using McpApi.Core.Secrets;
using McpApi.Core.Services;
using McpApi.Core.Storage;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Application Insights (optional - only if connection string is configured)
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration["applicationinsights-connection-string"];

if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
    Console.WriteLine("[INFO] Application Insights configured");
}
else
{
    Console.WriteLine("[INFO] Application Insights not configured (ApplicationInsights:ConnectionString not set)");
}

// Add Azure Key Vault configuration
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Configure rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Forwarded-For";
    options.ClientIdHeader = "X-ClientId";
    options.GeneralRules = new List<RateLimitRule>
    {
        // Global rate limit: 100 requests per minute per IP
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100
        },
        // Auth endpoints: 10 requests per minute per IP
        new RateLimitRule
        {
            Endpoint = "*:/api/auth/*",
            Period = "1m",
            Limit = 10
        },
        // Demo chat endpoint: 5 requests per minute per IP (prevent abuse)
        new RateLimitRule
        {
            Endpoint = "*:/api/demo/chat",
            Period = "1m",
            Limit = 5
        }
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

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

// Configure OAuth providers
var githubClientId = builder.Configuration["GitHub:ClientId"];
var githubClientSecret = builder.Configuration["GitHub:ClientSecret"];

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
})
.AddCookie("OAuthTemp", options =>
{
    options.Cookie.Name = "oauth_temp";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
});

// Add GitHub OAuth if configured
if (!string.IsNullOrEmpty(githubClientId) && !string.IsNullOrEmpty(githubClientSecret))
{
    authBuilder.AddGitHub("GitHub", options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.SignInScheme = "OAuthTemp";
        options.CallbackPath = "/api/auth/callback/github";
        options.SaveTokens = false;
        options.Scope.Add("user:email");
    });
    Console.WriteLine("[INFO] GitHub OAuth configured");
}
else
{
    Console.WriteLine("[WARNING] GitHub OAuth not configured (GitHub:ClientId and GitHub:ClientSecret required)");
}

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

// Configure auth service (OAuth-only, no email/SMS needed)
builder.Services.AddScoped<IAuthService>(sp =>
{
    var userStore = sp.GetRequiredService<IUserStore>();
    return new AuthService(userStore);
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
builder.Services.AddHttpClient<GitHubDemoService>();

// Configure demo services
var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"]
    ?? builder.Configuration["anthropic-api-key"];

if (!string.IsNullOrEmpty(anthropicApiKey))
{
    builder.Services.AddScoped<IGitHubDemoService, GitHubDemoService>();
}
else
{
    Console.WriteLine("[WARNING] Anthropic API key not configured. Demo endpoint will be disabled.");
    builder.Services.AddScoped<IGitHubDemoService>(_ => null!);
}

// Register parsers
builder.Services.AddTransient<IOpenApiParser>(sp => sp.GetRequiredService<OpenApiParser>());
builder.Services.AddSingleton<IAuthHandlerFactory>(sp =>
{
    var secretResolver = sp.GetRequiredService<ISecretResolver>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new AuthHandlerFactory(secretResolver, httpClientFactory);
});
builder.Services.AddSingleton<IApiClient, DynamicApiClient>();

// Configure health checks
builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        "cosmosdb",
        sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            return new CosmosDbHealthCheck(cosmosClient, databaseName);
        },
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "cosmos" }));

var app = builder.Build();

// Configure the HTTP request pipeline

// Global exception handler must be first to catch all unhandled exceptions
app.UseGlobalExceptionHandler();

// Security headers for all responses
app.UseSecurityHeaders();

// Rate limiting
app.UseIpRateLimiting();

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

// Health check endpoint (no authentication required)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(response);
    }
}).AllowAnonymous();

app.Run();
