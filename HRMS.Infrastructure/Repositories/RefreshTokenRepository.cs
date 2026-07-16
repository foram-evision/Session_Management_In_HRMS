using System.Data;
using Dapper;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HRMS.Infrastructure.Repositories;

/// <summary>
/// Refresh token / session repository — Dapper + PostgreSQL stored procedures.
/// All token parameters are SHA-256 hashes. Raw tokens are never seen or stored here.
/// PostgreSQL FUNCTIONS are called with SELECT * FROM, PROCEDURES are called with CommandType.StoredProcedure.
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly string _connectionString;

    public RefreshTokenRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    // ─── Add (Insert new session) ──────────────────────────────────────────────

    /// <summary>
    /// Inserts a new session record via sp_AddSession.
    /// All fields including TokenHash, SessionId, device metadata, and expiry timestamps are persisted.
    /// </summary>
    public async Task AddAsync(RefreshToken session)
    {
        const string procedureName = "sp_AddSession";
        using var connection = CreateConnection();
        await connection.ExecuteAsync(procedureName, new
        {
            p_id               = session.Id,
            p_session_id       = session.SessionId,
            p_token_hash       = session.TokenHash,
            p_expires          = session.Expires,
            p_absolute_expiry  = session.AbsoluteExpiry,
            p_created          = session.Created,
            p_created_by_ip    = session.CreatedByIp,
            p_browser          = session.Browser,
            p_device           = session.Device,
            p_operating_system = session.OperatingSystem,
            p_user_id          = session.UserId
        }, commandType: CommandType.StoredProcedure);
    }

    // ─── Get By Token Hash ─────────────────────────────────────────────────────

    /// <summary>
    /// Looks up a session by the SHA-256 hash of the refresh token.
    /// Called during RefreshToken and Logout operations.
    /// Returns null when no session matches the given hash.
    /// </summary>
    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        const string sql = "SELECT * FROM sp_GetSessionByTokenHash(@p_token_hash)";
        using var connection = CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql, new { p_token_hash = tokenHash });
        return row is null ? null : MapRowToSession(row);
    }

    // ─── Get By Session ID ─────────────────────────────────────────────────────

    /// <summary>
    /// Looks up a session by its unique SessionId.
    /// Called by the DELETE /api/auth/sessions/{sessionId} endpoint.
    /// Returns null when no session matches the given ID.
    /// </summary>
    public async Task<RefreshToken?> GetBySessionIdAsync(Guid sessionId)
    {
        const string sql = "SELECT * FROM sp_GetSessionBySessionId(@p_session_id)";
        using var connection = CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<SessionRow>(sql, new { p_session_id = sessionId });
        return row is null ? null : MapRowToSession(row);
    }

    // ─── Update (Rotation, Revocation, LastUsed) ──────────────────────────────

    /// <summary>
    /// Persists state changes to an existing session.
    /// Used for: token rotation (sets Revoked + ReplacedByTokenHash), logout (sets Revoked),
    /// and sliding expiry extension (sets LastUsed + Expires).
    /// </summary>
    public async Task UpdateAsync(RefreshToken session)
    {
        const string procedureName = "sp_UpdateSession";
        using var connection = CreateConnection();
        await connection.ExecuteAsync(procedureName, new
        {
            p_id                     = session.Id,
            p_revoked                = session.Revoked,
            p_revoked_by_ip          = session.RevokedByIp,
            p_last_used              = session.LastUsed,
            p_replaced_by_token_hash = session.ReplacedByTokenHash,
            p_revoke_reason          = session.RevokeReason
        }, commandType: CommandType.StoredProcedure);
    }

    // ─── Revoke All User Sessions ──────────────────────────────────────────────

    /// <summary>
    /// Revokes all active sessions for a user in a single database round-trip.
    /// Called by: LogoutAll, token reuse detection (security incident response).
    /// Only sessions that are not already revoked are affected.
    /// </summary>
    public async Task RevokeAllUserSessionsAsync(Guid userId, string revokedByIp, string reason)
    {
        const string procedureName = "sp_RevokeAllUserSessions";
        using var connection = CreateConnection();
        await connection.ExecuteAsync(procedureName, new
        {
            p_user_id       = userId,
            p_revoked_at    = DateTime.UtcNow,
            p_revoked_by_ip = revokedByIp,
            p_reason        = reason
        }, commandType: CommandType.StoredProcedure);
    }

    // ─── Get Active Sessions By User ───────────────────────────────────────────

    /// <summary>
    /// Returns all sessions that are currently active (not revoked, not expired)
    /// for the given user. Used by GET /api/auth/sessions.
    /// </summary>
    public async Task<IReadOnlyList<RefreshToken>> GetActiveSessionsByUserIdAsync(Guid userId)
    {
        const string sql = "SELECT * FROM sp_GetActiveSessionsByUserId(@p_user_id)";
        using var connection = CreateConnection();
        var rows = await connection.QueryAsync<SessionRow>(sql, new { p_user_id = userId });
        return rows.Select(MapRowToSession).ToList().AsReadOnly();
    }

    // ─── Internal Mapping ─────────────────────────────────────────────────────

    /// <summary>
    /// Internal flat row DTO that maps 1-to-1 with the columns returned by the PostgreSQL functions.
    /// Dapper maps column names by convention (case-insensitive in Npgsql by default).
    /// </summary>
    private sealed class SessionRow
    {
        public Guid      Id                  { get; init; }
        public Guid      SessionId           { get; init; }
        public string    TokenHash           { get; init; } = string.Empty;
        public DateTime  Expires             { get; init; }
        public DateTime  AbsoluteExpiry      { get; init; }
        public DateTime  Created             { get; init; }
        public DateTime? LastUsed            { get; init; }
        public DateTime? Revoked             { get; init; }
        public string?   CreatedByIp         { get; init; }
        public string?   RevokedByIp         { get; init; }
        public string?   Browser             { get; init; }
        public string?   Device              { get; init; }
        public string?   OperatingSystem     { get; init; }
        public string?   ReplacedByTokenHash { get; init; }
        public string?   RevokeReason        { get; init; }
        public Guid      UserId              { get; init; }
        // User join fields (present in sp_GetSessionByTokenHash and sp_GetSessionBySessionId)
        public string?   UserFullName        { get; init; }
        public string?   UserEmail           { get; init; }
        public Guid?     UserOrgId           { get; init; }
    }

    private static RefreshToken MapRowToSession(SessionRow row) => new()
    {
        Id                  = row.Id,
        SessionId           = row.SessionId,
        TokenHash           = row.TokenHash,
        Expires             = row.Expires,
        AbsoluteExpiry      = row.AbsoluteExpiry,
        Created             = row.Created,
        LastUsed            = row.LastUsed,
        Revoked             = row.Revoked,
        CreatedByIp         = row.CreatedByIp,
        RevokedByIp         = row.RevokedByIp,
        Browser             = row.Browser,
        Device              = row.Device,
        OperatingSystem     = row.OperatingSystem,
        ReplacedByTokenHash = row.ReplacedByTokenHash,
        RevokeReason        = row.RevokeReason,
        UserId              = row.UserId,
        User = new ApplicationUser
        {
            Id             = row.UserId,
            FullName       = row.UserFullName ?? string.Empty,
            Email          = row.UserEmail    ?? string.Empty,
            OrganizationId = row.UserOrgId
        }
    };
}
