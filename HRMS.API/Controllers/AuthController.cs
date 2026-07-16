using System.Security.Claims;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

/// <summary>
/// Authentication controller.
/// Endpoints:
///   POST   /api/auth/register                — create a new admin account
///   POST   /api/auth/login                   — authenticate and get token pair
///   POST   /api/auth/refresh-token           — rotate refresh token (anonymous)
///   POST   /api/auth/logout                  — revoke current session
///   POST   /api/auth/logout-all              — revoke all sessions (all devices)
///   GET    /api/auth/sessions                — list active sessions
///   DELETE /api/auth/sessions/{sessionId}    — revoke a specific session
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // ─── Register ─────────────────────────────────────────────────────────────

    /// <summary>Register a new admin user. No authentication required.</summary>
    /// <response code="200">Registration successful.</response>
    /// <response code="400">Validation failed or email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticate with email + password.
    /// Returns a JWT access token, a raw refresh token, and session metadata.
    /// A new session is created — existing sessions on other devices are NOT affected.
    /// </summary>
    /// <response code="200">Login successful. Token pair returned.</response>
    /// <response code="400">Invalid credentials.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    /// <summary>
    /// Exchange a valid refresh token for a new JWT + refresh token pair.
    /// The submitted refresh token is rotated (invalidated) and a new one is issued.
    /// Presenting a revoked token triggers token reuse detection — all sessions revoked.
    /// </summary>
    /// <response code="200">New token pair returned.</response>
    /// <response code="400">Invalid, expired, or reused token.</response>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.Token);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Logout (current device) ───────────────────────────────────────────────

    /// <summary>
    /// Revoke the current session. The JWT must be valid in the Authorization header.
    /// Only this device's session is revoked — other devices remain logged in.
    /// </summary>
    /// <response code="200">Session revoked successfully.</response>
    /// <response code="400">Token invalid or already revoked.</response>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        var result = await _authService.LogoutAsync(dto.Token);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Logout All Devices ────────────────────────────────────────────────────

    /// <summary>
    /// Revoke ALL active sessions for the authenticated user.
    /// Every device (laptop, phone, tablet) will be forced to log in again.
    /// </summary>
    /// <response code="200">All sessions revoked.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _authService.LogoutAllAsync(userId.Value);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Get Active Sessions ───────────────────────────────────────────────────

    /// <summary>
    /// Return all currently active sessions for the authenticated user.
    /// Useful for a "Manage Devices" screen.
    /// The current session is identified by the "sid" claim in the JWT.
    /// </summary>
    /// <response code="200">List of active sessions returned.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        // Read the session ID from the JWT "sid" claim to mark the current session
        var currentSessionIdStr = User.FindFirstValue("sid");
        Guid.TryParse(currentSessionIdStr, out var currentSessionId);

        var result = await _authService.GetActiveSessionsAsync(userId.Value, currentSessionId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Revoke Specific Session ───────────────────────────────────────────────

    /// <summary>
    /// Remotely revoke a specific session by its SessionId.
    /// You can only revoke sessions that belong to your own account.
    /// Use GET /api/auth/sessions first to discover SessionIds.
    /// </summary>
    /// <param name="sessionId">The SessionId of the session to revoke.</param>
    /// <response code="200">Session revoked successfully.</response>
    /// <response code="400">Session not found or already revoked.</response>
    /// <response code="401">Not authenticated or session belongs to another user.</response>
    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _authService.RevokeSessionAsync(sessionId, userId.Value);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the authenticated user's ID from the ClaimTypes.NameIdentifier JWT claim.
    /// Returns null if the claim is missing or cannot be parsed.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
