namespace HRMS.Domain.Enums;

/// <summary>
/// System role constants.
/// These values must exactly match the role names stored in ASP.NET Identity Roles.
/// </summary>
public static class UserRole
{
    /// <summary>
    /// System Administrator
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Human Resource
    /// </summary>
    public const string HR = "HR";

    /// <summary>
    /// Employee
    /// </summary>
    public const string Employee = "Employee";
}