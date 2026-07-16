using HRMS.Domain.Entities;

namespace HRMS.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization> CreateAsync(Organization organization);
    Task<List<Organization>> GetAllAsync();
    Task<Organization?> GetByIdAsync(Guid id);
    Task<Organization> UpdateAsync(Organization organization);
    Task DeleteAsync(Organization organization);
}
