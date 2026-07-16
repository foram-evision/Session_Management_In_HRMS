namespace HRMS.Application.DTOs.Employee;

/// <summary>
/// Response DTO for employee data returned from API endpoints.
/// </summary>
public class EmployeeResponseDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
}
