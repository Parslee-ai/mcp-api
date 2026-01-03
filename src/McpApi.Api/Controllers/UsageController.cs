using McpApi.Api.DTOs;
using McpApi.Api.Services;
using McpApi.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpApi.Api.Controllers;

[ApiController]
[Route("api/usage")]
[Authorize]
public class UsageController : ControllerBase
{
    private readonly IUsageTrackingService _usageService;
    private readonly ICurrentUserService _currentUser;

    public UsageController(IUsageTrackingService usageService, ICurrentUserService currentUser)
    {
        _usageService = usageService;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var userTier = await GetUserTierAsync(ct);

        var summary = await _usageService.GetUsageSummaryAsync(userId, userTier, ct);
        return Ok(summary.ToDto());
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int months = 12, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var history = await _usageService.GetUsageHistoryAsync(userId, months, ct);
        return Ok(history.Select(r => r.ToDto()));
    }

    private string GetRequiredUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated");
    }

    private async Task<string> GetUserTierAsync(CancellationToken ct)
    {
        var user = await _currentUser.GetCurrentUserAsync(ct);
        return user?.Tier ?? "free";
    }
}
