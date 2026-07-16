using System.Data;
using Dapper;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HRMS.Infrastructure.Repositories;

/// <summary>
/// Organization repository — Dapper + PostgreSQL Functions.
/// PostgreSQL FUNCTIONS are called with SELECT / SELECT * FROM, NOT with CALL.
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private readonly string _connectionString;

    public OrganizationRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    // ─── Create (CHANGED TO SP) ───────────────────────────────────────────────────
    public async Task<Organization> CreateAsync(Organization organization)
    {
        const string procedureName = "sp_CreateOrganization";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(procedureName, new
        {
            p_id = organization.Id,
            p_name = organization.Name,
            p_address = organization.Address,
            p_created_at = organization.CreatedAt
        }, commandType: CommandType.StoredProcedure);

        return organization;
    }

    // ─── Get All (KEPT AS FUNCTION) ───────────────────────────────────────────────
    public async Task<List<Organization>> GetAllAsync()
    {
        const string sql = "SELECT * FROM sp_GetAllOrganizations()";
        using var connection = CreateConnection();
        var rows = await connection.QueryAsync<OrganizationRow>(sql);
        return rows.Select(MapRowToOrganization).ToList();
    }

    public async Task<Organization?> GetByIdAsync(Guid id)
    {
        const string sql = "SELECT * FROM sp_GetOrganizationById(@p_id)";
        using var connection = CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<OrganizationRow>(sql, new { p_id = id });
        return row is null ? null : MapRowToOrganization(row);
    }

    public async Task<Organization> UpdateAsync(Organization organization)
    {
        const string procedureName = "sp_UpdateOrganization";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(procedureName, new
        {
            p_id = organization.Id,
            p_name = organization.Name,
            p_address = organization.Address
        }, commandType: CommandType.StoredProcedure);

        return organization;
    }

    public async Task DeleteAsync(Organization organization)
    {
        const string procedureName = "sp_DeleteOrganization";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(procedureName, new { p_id = organization.Id }, commandType: CommandType.StoredProcedure);
    }
    // ─── Mapping ──────────────────────────────────────────────────────────────

    private sealed class OrganizationRow
    {
        public Guid     Id        { get; init; }
        public string   Name      { get; init; } = string.Empty;
        public string   Address   { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    private static Organization MapRowToOrganization(OrganizationRow row) => new()
    {
        Id        = row.Id,
        Name      = row.Name,
        Address   = row.Address,
        CreatedAt = row.CreatedAt
    };
}
