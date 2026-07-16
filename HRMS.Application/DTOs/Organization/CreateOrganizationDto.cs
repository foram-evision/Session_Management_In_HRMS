namespace HRMS.Application.DTOs.Organization;

/// <summary>
/// DTO for creating a new organization. Authenticated user becomes the Admin.
/// </summary>
public class CreateOrganizationDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
