namespace HRMS.Application.DTOs.Auth;

/// <summary>
/// Response DTO for GET /api/auth/sessions.
/// Returned for each active session found in the database for the authenticated user.
/// The client can display this list in a "Manage Devices" screen.
/// </summary>
public class ActiveSessionDto
{
    /// <summary>Unique session identifier. Use this ID with DELETE /api/auth/sessions/{sessionId}.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Browser detected at login time (e.g., Chrome, Firefox, Edge).</summary>
    public string? Browser { get; set; }

    /// <summary>Device type detected at login time (Desktop, Mobile, Tablet).</summary>
    public string? Device { get; set; }

    /// <summary>Operating system detected at login time (Windows, macOS, iOS, Android).</summary>
    public string? OperatingSystem { get; set; }

    /// <summary>UTC timestamp when this session was first created (login time).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last successful token refresh. Null if never refreshed.</summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>IP address that created this session.</summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// True when this entry represents the caller's current session.
    /// Determined by comparing the "sid" JWT claim with each session's SessionId.
    /// </summary>
    public bool IsCurrentSession { get; set; }

    /// <summary>Human-readable status. Always "Active" for sessions returned by this endpoint.</summary>
    public string Status { get; set; } = "Active";
}
