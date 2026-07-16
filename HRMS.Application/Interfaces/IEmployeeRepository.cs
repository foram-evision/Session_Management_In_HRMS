using HRMS.Domain.Entities;

namespace HRMS.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee> CreateAsync(Employee employee);
    Task<Employee?> GetByIdAsync(Guid id);
    Task<List<Employee>> GetAllAsync(Guid? organizationId, Guid? createdBy);
    Task<Employee> UpdateAsync(Employee employee);
    Task DeleteAsync(Employee employee);

    // Duplicate Validation
    Task<Employee?> GetByEmailAsync(string email);
    Task<Employee?> GetByPhoneNumberAsync(string phoneNumber);
}
