using HRMS.Domain.Entities;

namespace HRMS.Application.Interfaces;

/// <summary>
/// Repository contract for managing refresh token sessions.
/// All "token" parameters that cross the boundary are SHA-256 hashes — never raw tokens.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Persist a new session record.
    /// The <see cref="RefreshToken.TokenHash"/> field must already contain the SHA-256 hash
    /// of the raw token — never pass the raw token here.
    /// </summary>
    Task AddAsync(RefreshToken session);

    /// <summary>
    /// Retrieve a session by the SHA-256 hash of the raw refresh token.
    /// Returns null when no matching session exists.
    /// Used during token refresh and logout operations.
    /// </summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

    /// <summary>
    /// Retrieve a session by its unique SessionId.
    /// Used by the revoke-specific-session endpoint.
    /// Returns null when no matching session exists.
    /// </summary>
    Task<RefreshToken?> GetBySessionIdAsync(Guid sessionId);

    /// <summary>
    /// Persist changes to a session (revocation fields, LastUsed, ReplacedByTokenHash, etc.).
    /// </summary>
    Task UpdateAsync(RefreshToken session);

    /// <summary>
    /// Revoke every active session belonging to the specified user in a single database call.
    /// Used by: LogoutAll, token reuse detection (security incident response).
    /// </summary>
    /// <param name="userId">Owner of the sessions to revoke.</param>
    /// <param name="revokedByIp">IP address that triggered the revocation.</param>
    /// <param name="reason">Reason stored in the RevokeReason column for audit purposes.</param>
    Task RevokeAllUserSessionsAsync(Guid userId, string revokedByIp, string reason);

    /// <summary>
    /// Return all sessions for a user that are currently active (not revoked and not expired).
    /// Used by the GET /api/auth/sessions endpoint.
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveSessionsByUserIdAsync(Guid userId);
}
