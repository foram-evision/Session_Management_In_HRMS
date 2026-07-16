namespace HRMS.Domain.Base;

/// <summary>
/// Base entity with UUID primary key. All domain entities inherit from this.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
