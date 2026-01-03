using McpApi.Api.DTOs;
using McpApi.Api.Services;
using McpApi.Core.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McpApi.Api.Controllers;

[ApiController]
[Route("api/tokens")]
[Authorize]
public class TokensController : ControllerBase
{
    private readonly IMcpTokenService _tokenService;
    private readonly ICurrentUserService _currentUser;

    public TokensController(IMcpTokenService tokenService, ICurrentUserService currentUser)
    {
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var tokens = await _tokenService.GetUserTokensAsync(userId, ct);
        return Ok(tokens.Select(t => t.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTokenRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var result = await _tokenService.CreateTokenAsync(userId, request.Name, request.ExpiresAt, ct);

        return CreatedAtAction(nameof(GetAll), new CreateTokenResponse(result.Token.ToDto(), result.PlaintextToken));
    }

    [HttpPut("{id}/revoke")]
    public async Task<IActionResult> Revoke(string id, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        try
        {
            await _tokenService.RevokeTokenAsync(userId, id, ct);
            return Ok(new MessageResponse("Token revoked successfully"));
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        try
        {
            await _tokenService.DeleteTokenAsync(userId, id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    private string GetRequiredUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User must be authenticated");
    }
}
