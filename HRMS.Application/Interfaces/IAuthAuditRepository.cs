using HRMS.Domain.Entities;

namespace HRMS.Application.Interfaces;

/// <summary>
/// Write-only repository for authentication audit logs.
/// Audit records are immutable — they are inserted once and never updated or deleted.
/// This interface is intentionally minimal to enforce the append-only pattern.
/// </summary>
public interface IAuthAuditRepository
{
    /// <summary>
    /// Append a new audit log entry to the database.
    /// Failures to write audit logs are swallowed silently in the service layer
    /// so that an audit write failure never breaks the primary auth flow.
    /// </summary>
    Task AddAsync(AuthAuditLog log);
}
