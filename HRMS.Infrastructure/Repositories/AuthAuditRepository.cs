using System.Data;
using Dapper;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HRMS.Infrastructure.Repositories;

/// <summary>
/// Audit log repository — append-only, Dapper + PostgreSQL.
/// Failures are logged but never rethrown so a DB hiccup can never break the primary auth flow.
/// </summary>
public class AuthAuditRepository : IAuthAuditRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AuthAuditRepository> _logger;

    public AuthAuditRepository(IConfiguration configuration, ILogger<AuthAuditRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Inserts an immutable audit record via sp_AddAuthAuditLog.
    /// Any exception during the insert is caught and logged so the calling service
    /// is never blocked by an audit write failure.
    /// </summary>
    public async Task AddAsync(AuthAuditLog log)
    {
        try
        {
            const string procedureName = "sp_AddAuthAuditLog";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(procedureName, new
            {
                p_id          = log.Id,
                p_user_id     = log.UserId,
                p_event       = log.Event,
                p_occurred_at = log.OccurredAt,
                p_ip_address  = log.IpAddress,
                p_user_agent  = log.UserAgent,
                p_device      = log.Device,
                p_browser     = log.Browser,
                p_details     = log.Details
            }, commandType: CommandType.StoredProcedure);
        }
        catch (Exception ex)
        {
            // Audit write failures must NEVER propagate — log and continue.
            _logger.LogError(ex, "Failed to write audit log. Event={Event}, UserId={UserId}", log.Event, log.UserId);
        }
    }
}
