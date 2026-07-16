using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;

namespace HRMS.Application.Interfaces;

/// <summary>
/// Contract for the authentication service layer.
/// All methods that require a client IP address obtain it internally via IHttpContextAccessor —
/// callers do not pass IP/User-Agent manually.
/// </summary>
public interface IAuthService
{
    // ─── Existing (kept, signatures unchanged) ────────────────────────────────

    /// <summary>Register a new admin user.</summary>
    Task<ApiResponse<bool>> RegisterAsync(RegisterDto dto);

    /// <summary>
    /// Authenticate user credentials and create a new session.
    /// Returns a JWT access token + raw refresh token pair.
    /// Does NOT revoke existing sessions — supports multi-device login.
    /// </summary>
    Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto dto);

    /// <summary>
    /// Rotate the refresh token. Validates, checks absolute expiry, detects reuse,
    /// revokes old token, issues new token with extended sliding expiry.
    /// </summary>
    Task<ApiResponse<TokenResponseDto>> RefreshTokenAsync(string token);

    /// <summary>Revoke the current session identified by the given raw refresh token.</summary>
    Task<ApiResponse<bool>> LogoutAsync(string token);

    // ─── New Enterprise Endpoints ─────────────────────────────────────────────

    /// <summary>
    /// Revoke every active session belonging to the given user.
    /// Useful for "sign out from all devices" functionality.
    /// </summary>
    /// <param name="userId">The authenticated user's ID (read from JWT claims in controller).</param>
    Task<ApiResponse<bool>> LogoutAllAsync(Guid userId);

    /// <summary>
    /// Return all currently active sessions for the user.
    /// Marks the session matching <paramref name="currentSessionId"/> as IsCurrentSession = true.
    /// </summary>
    /// <param name="userId">Authenticated user's ID.</param>
    /// <param name="currentSessionId">The session ID from the caller's JWT "sid" claim.</param>
    Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId, Guid currentSessionId);

    /// <summary>
    /// Revoke a specific session by its SessionId.
    /// Only the owner of the session (requestingUserId) may revoke it.
    /// </summary>
    /// <param name="sessionId">The SessionId to revoke.</param>
    /// <param name="requestingUserId">Must match the session's UserId — prevents cross-user revocation.</param>
    Task<ApiResponse<bool>> RevokeSessionAsync(Guid sessionId, Guid requestingUserId);
}
