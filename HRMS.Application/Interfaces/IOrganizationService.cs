using HRMS.Application.Common;
using HRMS.Application.DTOs.Organization;

namespace HRMS.Application.Interfaces;

public interface IOrganizationService
{
    Task<ApiResponse<OrganizationResponseDto>> CreateOrganizationAsync(CreateOrganizationDto dto, Guid userId);
    Task<ApiResponse<List<OrganizationResponseDto>>> GetAllOrganizationsAsync();
    Task<ApiResponse<OrganizationResponseDto>> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto dto, Guid userId);
    Task<ApiResponse<bool>> DeleteOrganizationAsync(Guid id, Guid userId);
}
