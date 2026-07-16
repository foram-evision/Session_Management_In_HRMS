using System.Data;
using Dapper;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HRMS.Infrastructure.Repositories;

/// <summary>
/// Employee Repository using Dapper + PostgreSQL.
/// Create, Update and Delete use Stored Procedures.
/// Read operations use PostgreSQL Functions.
/// </summary>
public class EmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;

    public EmployeeRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    private IDbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);

    // ==========================================================
    // CREATE
    // ==========================================================

    public async Task<Employee> CreateAsync(Employee employee)
    {
        using var connection = CreateConnection();

        await connection.ExecuteAsync(
            "sp_CreateEmployee",
            new
            {
                p_id = employee.Id,
                p_user_id = employee.UserId,
                p_first_name = employee.FirstName,
                p_last_name = employee.LastName,
                p_address = employee.Address,
                p_created_by = employee.CreatedBy,
                p_organization_id = employee.OrganizationId
            },
            commandType: CommandType.StoredProcedure);

        return employee;
    }

    // ==========================================================
    // GET BY ID
    // ==========================================================

    public async Task<Employee?> GetByIdAsync(Guid id)
    {
        const string sql = "SELECT * FROM sp_GetEmployeeById(@p_id)";

        using var connection = CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<EmployeeRow>(
            sql,
            new { p_id = id });

        return row is null ? null : MapRowToEmployee(row);
    }

    // ==========================================================
    // GET ALL
    // ==========================================================

    public async Task<List<Employee>> GetAllAsync(Guid? organizationId, Guid? createdBy)
    {
        const string sql = "SELECT * FROM sp_GetAllEmployees(@p_organization_id, @p_created_by)";

        using var connection = CreateConnection();

        var rows = await connection.QueryAsync<EmployeeRow>(
            sql,
            new
            {
                p_organization_id = organizationId,
                p_created_by = createdBy
            });

        return rows.Select(MapRowToEmployee).ToList();
    }

    // ==========================================================
    // UPDATE
    // ==========================================================

    public async Task<Employee> UpdateAsync(Employee employee)
    {
        const string procedureName = "sp_UpdateEmployee";

        using var connection = CreateConnection();

        await connection.ExecuteAsync(
            procedureName,
            new
            {
                p_id = employee.Id,
                p_first_name = employee.FirstName,
                p_last_name = employee.LastName,
                p_email = employee.User.Email,
                p_phone_number = employee.User.PhoneNumber,
                p_address = employee.Address
            },
            commandType: CommandType.StoredProcedure);

        return employee;
    }

    // ==========================================================
    // DELETE
    // ==========================================================

    public async Task DeleteAsync(Employee employee)
    {
        const string procedureName = "sp_DeleteEmployee";

        using var connection = CreateConnection();

        await connection.ExecuteAsync(
            procedureName,
            new
            {
                p_id = employee.Id
            },
            commandType: CommandType.StoredProcedure);
    }

    // ==========================================================
    // GET BY EMAIL
    // ==========================================================

    public async Task<Employee?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT
                e."Id",
                e."UserId",
                e."FirstName",
                e."LastName",
                e."Address",
                e."CreatedBy",
                e."OrganizationId",
                u."Email",
                u."PhoneNumber"
            FROM "Employees" e
            INNER JOIN "Users" u
                ON e."UserId" = u."Id"
            WHERE u."Email" = @Email;
            """;

        using var connection = CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<EmployeeRow>(
            sql,
            new { Email = email });

        return row == null ? null : MapRowToEmployee(row);
    }

    // ==========================================================
    // GET BY PHONE NUMBER
    // ==========================================================

    public async Task<Employee?> GetByPhoneNumberAsync(string phoneNumber)
    {
        const string sql = """
            SELECT
                e."Id",
                e."UserId",
                e."FirstName",
                e."LastName",
                e."Address",
                e."CreatedBy",
                e."OrganizationId",
                u."Email",
                u."PhoneNumber"
            FROM "Employees" e
            INNER JOIN "Users" u
                ON e."UserId" = u."Id"
            WHERE u."PhoneNumber" = @PhoneNumber;
            """;

        using var connection = CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<EmployeeRow>(
            sql,
            new { PhoneNumber = phoneNumber });

        return row == null ? null : MapRowToEmployee(row);
    }

    // ==========================================================
    // DATABASE RESULT MODEL
    // ==========================================================

    private sealed class EmployeeRow
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string Address { get; init; } = string.Empty;

        public Guid CreatedBy { get; init; }

        public Guid OrganizationId { get; init; }

        public string Email { get; init; } = string.Empty;

        public string PhoneNumber { get; init; } = string.Empty;

        public string? CreatedByName { get; init; }

        public string? OrganizationName { get; init; }
    }

    // ==========================================================
    // MAP DATABASE ROW TO ENTITY
    // ==========================================================

    private static Employee MapRowToEmployee(EmployeeRow row)
    {
        return new Employee
        {
            Id = row.Id,
            UserId = row.UserId,
            FirstName = row.FirstName,
            LastName = row.LastName,
            Address = row.Address,
            CreatedBy = row.CreatedBy,
            OrganizationId = row.OrganizationId,

            User = new ApplicationUser
            {
                Id = row.UserId,
                Email = row.Email,
                PhoneNumber = row.PhoneNumber
            },

            CreatedByUser = new ApplicationUser
            {
                FullName = row.CreatedByName ?? string.Empty
            },

            Organization = new Organization
            {
                Name = row.OrganizationName ?? string.Empty
            }
        };
    }
}