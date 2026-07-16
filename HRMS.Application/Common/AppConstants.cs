namespace HRMS.Application.Common;

/// <summary>
/// Application-wide constants for roles, standard messages, and audit event names.
/// </summary>
public static class AppConstants
{
    // ─── Roles ────────────────────────────────────────────────────────────────

    public static class Roles
    {
        // CHANGED TO ALL-CAPS TO MATCH THE JWT TOKEN AND DATABASE SEED DATA PERFECTLY
        public const string Admin = "ADMIN";
        public const string HR = "HR";
    }

    // ─── Standard API Messages ────────────────────────────────────────────────

    public static class Messages
    {
        // Existing messages (unchanged — do not break any existing callers)
        public const string UserNotFound = "User not found.";
        public const string InvalidCredentials = "Invalid email or password.";
        public const string OrganizationNotFound = "Organization not found.";
        public const string EmployeeNotFound = "Employee not found.";
        public const string Unauthorized = "You are not authorized to perform this action.";
        public const string UserAlreadyExists = "A user with this email already exists.";
        public const string OrganizationAlreadyExists = "You already belong to an organization. Cannot create another.";
        public const string InvalidToken = "Invalid or expired token.";
        public const string NoOrganization = "User does not belong to any organization.";

        // New enterprise auth messages
        /// <summary>
        /// Returned when a previously rotated (used) refresh token is presented again.
        /// Indicates a potential token theft — all sessions are revoked as a security measure.
        /// </summary>
        public const string TokenReuseDetected =
            "Security violation detected: a revoked token was reused. All your sessions have been terminated for safety. Please log in again.";

        /// <summary>Returned when the session's absolute maximum lifetime has been exceeded.</summary>
        public const string SessionAbsoluteExpired =
            "Your session has reached its maximum lifetime. Please log in again.";

        /// <summary>Returned when a session lookup by SessionId returns no result.</summary>
        public const string SessionNotFound = "Session not found.";

        /// <summary>Returned when trying to revoke a session that is already revoked.</summary>
        public const string SessionAlreadyRevoked = "Session is already revoked.";

        /// <summary>Returned when a user tries to revoke a session belonging to another user.</summary>
        public const string SessionUnauthorized = "You are not authorized to revoke this session.";
    }

    // ─── Audit Event Names ────────────────────────────────────────────────────

    /// <summary>
    /// Strongly-typed constants for the Event column in AuthAuditLogs.
    /// Using constants prevents typo-based inconsistencies across the codebase.
    /// </summary>
    public static class AuditEvents
    {
        /// <summary>Successful login — new session created.</summary>
        public const string Login = "Login";

        /// <summary>Failed login attempt — invalid credentials.</summary>
        public const string LoginFailed = "LoginFailed";

        /// <summary>Successful token refresh — session rotated.</summary>
        public const string TokenRefreshed = "TokenRefreshed";

        /// <summary>User explicitly logged out from the current device.</summary>
        public const string Logout = "Logout";

        /// <summary>User explicitly logged out from all devices.</summary>
        public const string LogoutAll = "LogoutAll";

        /// <summary>A specific session was revoked via the sessions API.</summary>
        public const string SessionRevoked = "SessionRevoked";

        /// <summary>
        /// A previously revoked refresh token was presented — token theft suspected.
        /// All user sessions are revoked in response.
        /// </summary>
        public const string TokenReuseDetected = "TokenReuseDetected";

        /// <summary>A session expired (sliding or absolute) and was cleaned up.</summary>
        public const string SessionExpired = "SessionExpired";
    }
}