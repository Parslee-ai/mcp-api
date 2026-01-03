namespace McpApi.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Secret { get; set; }
    public string Issuer { get; set; } = "McpApi";
    public string Audience { get; set; } = "McpApi";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
