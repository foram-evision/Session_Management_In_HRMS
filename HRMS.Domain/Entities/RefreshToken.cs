using HRMS.Domain.Base;

namespace HRMS.Domain.Entities;

/// <summary>
/// Enterprise session record. Every successful login creates exactly one session.
/// The raw refresh token is NEVER stored — only its SHA-256 hash is persisted.
/// Supports multi-device, sliding expiry, absolute expiry, and token reuse detection.
/// Maps to the "RefreshTokens" table.
/// </summary>
public class RefreshToken : BaseEntity
{
    // ─── Token Security ───────────────────────────────────────────────────────

    /// <summary>
    /// SHA-256 hash of the raw refresh token.
    /// The raw token is returned to the client once and then discarded from server memory.
    /// Even if the database is breached, the raw tokens cannot be recovered.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this session.
    /// Included as the "sid" claim inside the JWT so any session can be revoked
    /// without re-logging in on all devices.
    /// </summary>
    public Guid SessionId { get; set; } = Guid.NewGuid();

    // ─── Expiry ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sliding expiry — reset on every successful refresh.
    /// Default: 30 days (configurable via JwtSettings:SlidingExpiryDays).
    /// </summary>
    public DateTime Expires { get; set; }

    /// <summary>
    /// Absolute maximum session lifetime — NEVER extended regardless of activity.
    /// Forces a full re-login after this date (default: 90 days).
    /// Configured via JwtSettings:AbsoluteExpiryDays.
    /// </summary>
    public DateTime AbsoluteExpiry { get; set; }

    /// <summary>Session creation timestamp (login time).</summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated on every successful token refresh.
    /// Indicates the last time the user actively used this session.
    /// </summary>
    public DateTime? LastUsed { get; set; }

    // ─── Revocation ───────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when this session was revoked. Null = not revoked.</summary>
    public DateTime? Revoked { get; set; }

    /// <summary>
    /// Hash of the new token that replaced this one during rotation.
    /// Enables forward-chaining for reuse detection forensics.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>
    /// Human-readable reason this session was revoked.
    /// Examples: "Rotated", "Logout", "LogoutAll", "TokenReuse", "AbsoluteExpiry".
    /// </summary>
    public string? RevokeReason { get; set; }

    // ─── Device / Client Metadata ─────────────────────────────────────────────

    /// <summary>IP address that created this session (supports X-Forwarded-For).</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>IP address that performed the revocation action.</summary>
    public string? RevokedByIp { get; set; }

    /// <summary>Browser name detected from the User-Agent header (e.g., Chrome, Firefox).</summary>
    public string? Browser { get; set; }

    /// <summary>Device type detected from the User-Agent header (Desktop, Mobile, Tablet).</summary>
    public string? Device { get; set; }

    /// <summary>Operating system detected from the User-Agent header (e.g., Windows, iOS).</summary>
    public string? OperatingSystem { get; set; }

    // ─── Foreign Key ──────────────────────────────────────────────────────────

    /// <summary>FK → Users.Id. One user can have many concurrent sessions (multi-device).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation property to the owning user.</summary>
    public ApplicationUser User { get; set; } = null!;

    // ─── Computed Helpers (not mapped to DB) ──────────────────────────────────

    /// <summary>True if the sliding expiry window has passed.</summary>
    public bool IsExpired => DateTime.UtcNow >= Expires;

    /// <summary>True if the absolute maximum session lifetime has passed.</summary>
    public bool IsAbsolutelyExpired => DateTime.UtcNow >= AbsoluteExpiry;

    /// <summary>True if the session was explicitly revoked.</summary>
    public bool IsRevoked => Revoked != null;

    /// <summary>
    /// True when the session is fully valid and can be used to issue new access tokens.
    /// A session is active only when it is NOT revoked AND NOT sliding-expired AND NOT absolutely-expired.
    /// </summary>
    public bool IsActive => !IsRevoked && !IsExpired && !IsAbsolutelyExpired;
}
