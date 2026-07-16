namespace HRMS.Application.DTOs.Organization;

/// <summary>
/// Response DTO for organization data.
/// </summary>
public class OrganizationResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
