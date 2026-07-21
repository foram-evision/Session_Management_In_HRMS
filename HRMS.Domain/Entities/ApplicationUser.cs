using Microsoft.AspNetCore.Identity;
using System;

namespace HRMS.Domain.Entities;

/// <summary>
/// Represents every authenticated user in the system.
/// Admin, HR and Employee are all stored in this table.
/// Roles are managed through ASP.NET Identity Roles.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// User full name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Organization to which the user belongs.
    /// </summary>
    public Guid? OrganizationId { get; set; }
    public string Address { get; set; } = string.Empty;
    /// <summary>
    /// Indicates whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date and time when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the user was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// User who created this record.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// User who last updated this record.
    /// </summary>
    public Guid? UpdatedByUserId { get; set; }

    // Navigation Properties

    public virtual Organization? Organization { get; set; }

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}