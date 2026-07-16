namespace HRMS.Application.DTOs.Employee;

/// <summary>
/// DTO for HR to create a new employee.
/// Fields match exactly: FirstName, LastName, Email, PhoneNumber, Address.
/// </summary>
public class CreateEmployeeDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
