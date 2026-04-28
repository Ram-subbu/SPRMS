using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SPRMS.Common;
using SPRMS.Services.Domain;

namespace SPRMS.Controllers;

/// <summary>
/// Authentication and authorization endpoints.
/// Handles user login, logout, token refresh, and related operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController(IAuthService authSvc) : BaseController
{
    /// <summary>
    /// User login with credentials.
    /// </summary>
    /// <param name="request">Login credentials (CID and password)</param>
    /// <returns>Authentication token and user details</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]  // 5 requests per 5 minutes
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationError(ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList());

        if (string.IsNullOrWhiteSpace(request.Cid) || string.IsNullOrWhiteSpace(request.Password))
            return ValidationError(new List<string> { "CID and password are required." });

        var result = await authSvc.AuthenticateAsync(request.Cid, request.Password, ct);
        if (!result.Success)
            return Unauthorized(result);

        return Success(result.Data);
    }

    /// <summary>
    /// NDI-based login (Bhutan national ID integration).
    /// </summary>
    /// <param name="request">NDI login request</param>
    /// <returns>Authentication token via NDI</returns>
    [HttpPost("ndi-login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> NdiLogin([FromBody] NdiLoginRequest request)
    {
        // TODO: Implement NDI integration
        return Unauthorized(new { message = "NDI login not yet implemented." });
    }

    /// <summary>
    /// Verify 2FA token (TOTP).
    /// </summary>
    /// <param name="request">2FA verification request</param>
    /// <returns>Verified token</returns>
    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify2FA([FromBody] TwoFAVerifyRequest request)
    {
        // TODO: Implement 2FA verification
        return Unauthorized(new { message = "2FA verification not yet implemented." });
    }

    /// <summary>
    /// Refresh JWT access token.
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New access token</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ValidationError(new List<string> { "Refresh token is required." });

        var result = await authSvc.RefreshTokenAsync(request.RefreshToken, ct);
        if (!result.Success)
            return Unauthorized(result);

        return Success(result.Data);
    }

    /// <summary>
    /// User logout.
    /// </summary>
    /// <returns>Logout confirmation</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken ct = default)
    {
        // Extract user ID from claims or context
        var userIdClaim = User.FindFirst("uid");
        if (!long.TryParse(userIdClaim?.Value, out long userId))
            return Failed("Invalid user context");

        var result = await authSvc.LogoutAsync(userId, ct);
        return result.Success ? Success(result) : Failed(result.Message ?? "Logout failed", result.ErrorCode);
    }
}

/// <summary>Request models for authentication endpoints.</summary>
public record LoginRequest(string Cid, string Password);
public record NdiLoginRequest(string NdiToken);
public record TwoFAVerifyRequest(string Token, string TotpCode);
public record RefreshTokenRequest(string RefreshToken);
