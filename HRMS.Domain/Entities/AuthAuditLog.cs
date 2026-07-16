namespace HRMS.Domain.Entities;

/// <summary>
/// Immutable audit record for every security-significant authentication event.
/// Written on: Login, LoginFailed, TokenRefreshed, Logout, LogoutAll,
/// SessionRevoked, TokenReuseDetected, SessionExpired.
/// Records are never updated or deleted — they form a tamper-evident audit trail.
/// Maps to the "AuthAuditLogs" table.
/// </summary>
public class AuthAuditLog
{
    /// <summary>Unique record identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user who triggered this event.
    /// Nullable because LoginFailed events may have no valid UserId
    /// (e.g., email does not exist in the system).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Event type constant. Use <see cref="HRMS.Application.Common.AppConstants.AuditEvents"/>
    /// for all valid values to ensure consistency.
    /// </summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the event occurred.</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Client IP address at the time of the event.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Raw User-Agent header string for forensic reference.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Device type parsed from User-Agent (Desktop / Mobile / Tablet).</summary>
    public string? Device { get; set; }

    /// <summary>Browser name parsed from User-Agent (Chrome, Firefox, Edge, etc.).</summary>
    public string? Browser { get; set; }

    /// <summary>
    /// Free-form supplementary detail for the event.
    /// Examples: SessionId reference, failure reason, number of sessions revoked.
    /// </summary>
    public string? Details { get; set; }
}
