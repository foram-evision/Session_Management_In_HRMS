using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;

namespace HRMS.Application.Interfaces;

public interface IEmployeeService
{
    Task<ApiResponse<List<UserListItemDto>>> GetUsersAsync(Guid callerId, string callerRole, string? roleFilter);
    Task<ApiResponse<UserListItemDto>> GetUserByIdAsync(Guid id, Guid callerId, string callerRole);
    Task<ApiResponse<UserListItemDto>> CreateUserAsync(CreateUserDto dto, Guid callerId, string callerRole);
    Task<ApiResponse<UserListItemDto>> UpdateUserAsync(Guid id, UpdateUserDto dto, Guid callerId, string callerRole);
    Task<ApiResponse<bool>> DeleteUserAsync(Guid id, Guid callerId, string callerRole);
}
