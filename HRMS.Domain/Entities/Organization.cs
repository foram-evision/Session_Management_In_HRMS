using HRMS.Domain.Base;

namespace HRMS.Domain.Entities;

/// <summary>
/// Represents an organization in the HRMS.
/// One organization can have multiple users and employees.
/// </summary>
public class Organization : BaseEntity
{
    /// <summary>
    /// Organization name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Organization address.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the organization is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates whether the organization has been soft deleted.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Record creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Record last updated date.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties

    /// <summary>
    /// All users belonging to this organization.
    /// </summary>
    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    /// <summary>
    /// All employees belonging to this organization.
    /// </summary>
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}