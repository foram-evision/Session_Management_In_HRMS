namespace HRMS.Application.DTOs.Auth;

/// <summary>
/// Response DTO returned on successful login or token refresh.
/// Contains the JWT access token, a new raw refresh token, and session metadata.
/// </summary>
public class TokenResponseDto
{
    /// <summary>Short-lived JWT access token. Include in the Authorization header as "Bearer {token}".</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Raw refresh token (plain text, base-64 encoded).
    /// Store securely on the client (HttpOnly cookie or secure storage).
    /// Use with POST /api/auth/refresh-token to obtain a new access token.
    /// This value is NEVER stored server-side — only its SHA-256 hash is persisted.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the session that issued this token pair.
    /// Use with DELETE /api/auth/sessions/{sessionId} to revoke this specific session.
    /// Also present as the "sid" claim inside the JWT.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>UTC expiry time of the access token (NOT the refresh token).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Authenticated user's full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Authenticated user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Authenticated user's unique identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Organization the user belongs to. Null if not yet assigned to an organization.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>List of roles assigned to the authenticated user.</summary>
    public List<string> Roles { get; set; } = new();
}
