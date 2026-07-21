using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HRMS.Application.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        UserManager<ApplicationUser> userManager)
    {
        _employeeRepository = employeeRepository;
        _userManager = userManager;
    }

    public async Task<ApiResponse<UserListItemDto>> CreateUserAsync(CreateUserDto dto, Guid callerId, string callerRole)
    {
        var callerUser = await _userManager.FindByIdAsync(callerId.ToString());
        if (callerUser == null)
            return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.UserNotFound);

        if (!callerUser.OrganizationId.HasValue)
            return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.NoOrganization);

        // ==========================================================
        // CREATE HR USER FLOW
        // ==========================================================
        if (callerRole == AppConstants.Roles.Admin && dto.TargetRole == AppConstants.Roles.HR)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.UserAlreadyExists);

            var hrUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FullName = $"{dto.FirstName} {dto.LastName}".Trim(),
                Email = dto.Email,
                UserName = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,

                NormalizedEmail = dto.Email.ToUpperInvariant(),
                NormalizedUserName = dto.Email.ToUpperInvariant(),

                OrganizationId = callerUser.OrganizationId.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = callerId
            };


            var result = await _userManager.CreateAsync(hrUser, dto.Password);

            if (!result.Succeeded)
            {
                return ApiResponse<UserListItemDto>.Failure(
                    "Failed to create HR user.",
                    result.Errors.Select(e => e.Description).ToList());
            }

            // Reload user from database
            var savedUser = await _userManager.FindByEmailAsync(dto.Email);

            Console.WriteLine("====================================");
            Console.WriteLine($"DTO Address     : {dto.Address}");
            Console.WriteLine($"Object Address  : {hrUser.Address}");
            Console.WriteLine($"Database Address: {savedUser?.Address}");
            Console.WriteLine("====================================");


            if (!result.Succeeded)
                return ApiResponse<UserListItemDto>.Failure("Failed to create HR user.", result.Errors.Select(e => e.Description).ToList());

            var roleResult = await _userManager.AddToRoleAsync(hrUser, AppConstants.Roles.HR);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(hrUser);
                return ApiResponse<UserListItemDto>.Failure("Failed to assign HR role.", roleResult.Errors.Select(e => e.Description).ToList());
            }

            return ApiResponse<UserListItemDto>.SuccessResult(MapToDto(hrUser, callerUser.FullName), "HR user created successfully.");
        }

        // ==========================================================
        // CREATE EMPLOYEE PROFILE + APPLICATION USER LINKED FLOW
        // ==========================================================
        else if (callerRole == AppConstants.Roles.HR && dto.TargetRole == "Employee")
        {
            dto.PhoneNumber = dto.PhoneNumber.Replace(" ", "");

            var existingEmail = await _employeeRepository.GetByEmailAsync(dto.Email);
            var existingPhone = await _employeeRepository.GetByPhoneNumberAsync(dto.PhoneNumber);
            var errors = new List<string>();

            if (existingEmail != null) errors.Add("Email already exists.");
            if (existingPhone != null) errors.Add("Phone number already exists.");
            if (errors.Any()) return ApiResponse<UserListItemDto>.Failure(string.Join(" ", errors));

            var employeeUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FullName = $"{dto.FirstName} {dto.LastName}".Trim(),
                Email = dto.Email,
                UserName = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                NormalizedEmail = dto.Email.ToUpperInvariant(),
                NormalizedUserName = dto.Email.ToUpperInvariant(),
                OrganizationId = callerUser.OrganizationId.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = callerId
            };

            var userResult = await _userManager.CreateAsync(employeeUser, dto.Password);
            if (!userResult.Succeeded)
            {
                return ApiResponse<UserListItemDto>.Failure("Failed to create identity user for employee.", userResult.Errors.Select(e => e.Description).ToList());
            }

            var roleResult = await _userManager.AddToRoleAsync(employeeUser, "Employee");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(employeeUser);
                return ApiResponse<UserListItemDto>.Failure("Failed to assign Employee role.", userResult.Errors.Select(e => e.Description).ToList());
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                UserId = employeeUser.Id,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Address = dto.Address,
                CreatedBy = callerId,
                OrganizationId = callerUser.OrganizationId.Value,
                User = employeeUser
            };

            var created = await _employeeRepository.CreateAsync(employee);
            return ApiResponse<UserListItemDto>.SuccessResult(MapToDto(created, callerUser.FullName), "Employee created successfully.");
        }

        return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.Unauthorized);
    }

    public async Task<ApiResponse<List<UserListItemDto>>> GetUsersAsync(Guid callerId, string callerRole, string? roleFilter)
    {
        var callerUser = await _userManager.FindByIdAsync(callerId.ToString());
        if (callerUser == null)
            return ApiResponse<List<UserListItemDto>>.Failure(AppConstants.Messages.UserNotFound);

        if (!callerUser.OrganizationId.HasValue)
            return ApiResponse<List<UserListItemDto>>.Failure(AppConstants.Messages.NoOrganization);

        var dtos = new List<UserListItemDto>();

        // Define exact allowed system roles for validation matching
        var validSystemRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppConstants.Roles.Admin,
            AppConstants.Roles.HR,
            "Employee"
        };

        var targetRoles = new List<string>();

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            // Clean parsing: split by commas, wipe surrounding whitespace, and strip nulls
            var rawRoles = roleFilter.Split(',')
                                     .Select(r => r.Trim())
                                     .Where(r => !string.IsNullOrEmpty(r))
                                     .ToList();

            // Guard check: Throw error back if any user input completely misses valid system structures
            var invalidRoles = rawRoles.Where(r => !validSystemRoles.Contains(r)).ToList();
            if (invalidRoles.Any())
            {
                return ApiResponse<List<UserListItemDto>>.Failure(
                    $"Invalid role filter parameter(s) detected: '{string.Join(", ", invalidRoles)}'. " +
                    "Please format your request using only allowed system roles: 'Admin', 'HR', or 'Employee' (separated by commas if filtering multiple roles)."
                );
            }

            // Maps safe user strings back into uniform C# layout casings
            foreach (var rawRole in rawRoles)
            {
                if (string.Equals(rawRole, AppConstants.Roles.Admin, StringComparison.OrdinalIgnoreCase))
                    targetRoles.Add(AppConstants.Roles.Admin);
                else if (string.Equals(rawRole, AppConstants.Roles.HR, StringComparison.OrdinalIgnoreCase))
                    targetRoles.Add(AppConstants.Roles.HR);
                else if (string.Equals(rawRole, "Employee", StringComparison.OrdinalIgnoreCase))
                    targetRoles.Add("Employee");
            }
        }

        // Default query fallback if parameter field stands empty
        // Role filter is mandatory
        if (string.IsNullOrWhiteSpace(roleFilter))
        {
            return ApiResponse<List<UserListItemDto>>.Failure(
                "Role filter is required. Please provide one or more roles. Allowed values: Admin, HR, Employee."
            );
        }

        // Process Identity Layer Roles (Admin / HR)
        if (targetRoles.Contains(AppConstants.Roles.Admin) || targetRoles.Contains(AppConstants.Roles.HR))
        {
            var usersInOrg = _userManager.Users.Where(u => u.OrganizationId == callerUser.OrganizationId).ToList();
            foreach (var u in usersInOrg)
            {
                var creator = u.CreatedByUserId.HasValue ? await _userManager.FindByIdAsync(u.CreatedByUserId.Value.ToString()) : null;
                var creatorName = creator?.FullName ?? "System Setup";

                if (targetRoles.Contains(AppConstants.Roles.Admin) && await _userManager.IsInRoleAsync(u, AppConstants.Roles.Admin))
                {
                    dtos.Add(MapToDtoWithExplicitRole(u, creatorName, AppConstants.Roles.Admin));
                }

                if (targetRoles.Contains(AppConstants.Roles.HR) && await _userManager.IsInRoleAsync(u, AppConstants.Roles.HR))
                {
                    dtos.Add(MapToDtoWithExplicitRole(u, creatorName, AppConstants.Roles.HR));
                }
            }
        }

        // Process Custom Entities Data Layer (Employee records)
        if (targetRoles.Contains("Employee"))
        {
            List<Employee> employees = (callerRole == AppConstants.Roles.Admin)
                ? await _employeeRepository.GetAllAsync(callerUser.OrganizationId, null)
                : await _employeeRepository.GetAllAsync(null, callerId);

            foreach (var emp in employees)
            {
                var creator = await _userManager.FindByIdAsync(emp.CreatedBy.ToString());
                dtos.Add(MapToDto(emp, creator?.FullName ?? "Unknown"));
            }
        }

        return ApiResponse<List<UserListItemDto>>.SuccessResult(dtos);
    }

    public async Task<ApiResponse<UserListItemDto>> GetUserByIdAsync(Guid id, Guid callerId, string callerRole)
    {
        var employee = await _employeeRepository.GetByIdAsync(id);
        if (employee != null)
        {
            if (callerRole == AppConstants.Roles.HR && employee.CreatedBy != callerId)
                return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.Unauthorized);

            var creator = await _userManager.FindByIdAsync(employee.CreatedBy.ToString());
            return ApiResponse<UserListItemDto>.SuccessResult(MapToDto(employee, creator?.FullName ?? "Unknown"));
        }

        var identityUser = await _userManager.FindByIdAsync(id.ToString());
        if (identityUser != null)
        {
            var callerUser = await _userManager.FindByIdAsync(callerId.ToString());
            if (identityUser.OrganizationId != callerUser?.OrganizationId)
                return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.Unauthorized);

            string assignedRole;

            if (await _userManager.IsInRoleAsync(identityUser, AppConstants.Roles.Admin))
            {
                assignedRole = AppConstants.Roles.Admin;
            }
            else if (await _userManager.IsInRoleAsync(identityUser, AppConstants.Roles.HR))
            {
                assignedRole = AppConstants.Roles.HR;
            }
            else if (await _userManager.IsInRoleAsync(identityUser, "Employee"))
            {
                assignedRole = "Employee";
            }
            else
            {
                assignedRole = "Unknown";
            }

            var creator = identityUser.CreatedByUserId.HasValue ? await _userManager.FindByIdAsync(identityUser.CreatedByUserId.Value.ToString()) : null;
            return ApiResponse<UserListItemDto>.SuccessResult(MapToDtoWithExplicitRole(identityUser, creator?.FullName ?? "Unknown", assignedRole));
        }

        return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.EmployeeNotFound);
    }

    public async Task<ApiResponse<UserListItemDto>> UpdateUserAsync(Guid id, UpdateUserDto dto, Guid callerId, string callerRole)
    {
        if (callerRole == AppConstants.Roles.Admin)
        {
            var hrUser = await _userManager.FindByIdAsync(id.ToString());
            if (hrUser == null || !await _userManager.IsInRoleAsync(hrUser, AppConstants.Roles.HR))
                return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.UserNotFound);

            var callerUser = await _userManager.FindByIdAsync(callerId.ToString());
            if (hrUser.OrganizationId != callerUser?.OrganizationId) return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.Unauthorized);

            hrUser.FullName = $"{dto.FirstName} {dto.LastName}".Trim();

            hrUser.Email = dto.Email;
            hrUser.UserName = dto.Email;
            hrUser.NormalizedEmail = dto.Email.ToUpperInvariant();
            hrUser.NormalizedUserName = dto.Email.ToUpperInvariant();
            hrUser.PhoneNumber = dto.PhoneNumber;
            hrUser.Address = dto.Address;

            hrUser.UpdatedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(hrUser);
            var creator = hrUser.CreatedByUserId.HasValue ? await _userManager.FindByIdAsync(hrUser.CreatedByUserId.Value.ToString()) : null;
            return ApiResponse<UserListItemDto>.SuccessResult(MapToDto(hrUser, creator?.FullName ?? "Unknown"), "HR user updated successfully.");
        }
        else if (callerRole == AppConstants.Roles.HR)
        {
            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null) return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.EmployeeNotFound);

            if (employee.CreatedBy != callerId) return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.Unauthorized);

            dto.PhoneNumber = dto.PhoneNumber.Replace(" ", "");

            var existingEmail = await _employeeRepository.GetByEmailAsync(dto.Email);
            var existingPhone = await _employeeRepository.GetByPhoneNumberAsync(dto.PhoneNumber);
            var errors = new List<string>();

            if (existingEmail != null && existingEmail.Id != employee.Id) errors.Add("Email already exists.");
            if (existingPhone != null && existingPhone.Id != employee.Id) errors.Add("Phone number already exists.");
            if (errors.Any()) return ApiResponse<UserListItemDto>.Failure(string.Join(" ", errors));

            var identityUser = await _userManager.FindByIdAsync(employee.UserId.ToString());
            if (identityUser != null)
            {
                identityUser.Email = dto.Email;
                identityUser.UserName = dto.Email;
                identityUser.NormalizedEmail = dto.Email.ToUpperInvariant();
                identityUser.NormalizedUserName = dto.Email.ToUpperInvariant();
                identityUser.PhoneNumber = dto.PhoneNumber;
                identityUser.Address = dto.Address;
                identityUser.FullName = $"{dto.FirstName} {dto.LastName}".Trim();
                identityUser.UpdatedAt = DateTime.UtcNow;

                await _userManager.UpdateAsync(identityUser);
            }

            employee.FirstName = dto.FirstName;
            employee.LastName = dto.LastName;
            employee.Address = dto.Address;

            var updated = await _employeeRepository.UpdateAsync(employee);
            updated.User = identityUser;

            var creator = await _userManager.FindByIdAsync(updated.CreatedBy.ToString());
            return ApiResponse<UserListItemDto>.SuccessResult(MapToDto(updated, creator?.FullName ?? "Unknown"), "Employee updated successfully.");
        }

        return ApiResponse<UserListItemDto>.Failure(AppConstants.Messages.Unauthorized);
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id, Guid callerId, string callerRole)
    {
        if (callerRole == AppConstants.Roles.Admin)
        {
            var hrUser = await _userManager.FindByIdAsync(id.ToString());
            if (hrUser == null || !await _userManager.IsInRoleAsync(hrUser, AppConstants.Roles.HR))
                return ApiResponse<bool>.Failure("HR user not found.");

            var callerUser = await _userManager.FindByIdAsync(callerId.ToString());
            if (hrUser.OrganizationId != callerUser?.OrganizationId) return ApiResponse<bool>.Failure(AppConstants.Messages.Unauthorized);

            var result = await _userManager.DeleteAsync(hrUser);
            if (!result.Succeeded) return ApiResponse<bool>.Failure("Failed to delete HR user.", result.Errors.Select(e => e.Description).ToList());

            return ApiResponse<bool>.SuccessResult(true, "HR user deleted successfully.");
        }
        else if (callerRole == AppConstants.Roles.HR)
        {
            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null) return ApiResponse<bool>.Failure(AppConstants.Messages.EmployeeNotFound);

            if (employee.CreatedBy != callerId) return ApiResponse<bool>.Failure(AppConstants.Messages.Unauthorized);

            var employeeUser = await _userManager.FindByIdAsync(employee.UserId.ToString());

            await _employeeRepository.DeleteAsync(employee);

            if (employeeUser != null)
            {
                await _userManager.DeleteAsync(employeeUser);
            }

            return ApiResponse<bool>.SuccessResult(true, "Employee deleted successfully.");
        }

        return ApiResponse<bool>.Failure(AppConstants.Messages.Unauthorized);
    }

    private static UserListItemDto MapToDto(ApplicationUser user, string creatorName)
    {
        return MapToDtoWithExplicitRole(user, creatorName, AppConstants.Roles.HR);
    }

    private static UserListItemDto MapToDtoWithExplicitRole(
    ApplicationUser user,
    string creatorName,
    string role)
    {
        var nameParts = (user.FullName ?? string.Empty).Split(' ', 2);

        return new UserListItemDto
        {
            Id = user.Id,
            Role = role,
            FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
            LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,

            // Return the real address stored in ApplicationUser
            Address = user.Address,
            CreatedBy = user.CreatedByUserId,
            CreatedByName = creatorName,
            OrganizationId = user.OrganizationId ?? Guid.Empty,
            CreatedAt = user.CreatedAt
        };
    }

    private static UserListItemDto MapToDto(Employee emp, string creatorName) => new()
    {
        Id = emp.Id,
        Role = "Employee",
        FirstName = emp.FirstName,
        LastName = emp.LastName,
        Email = emp.User?.Email ?? string.Empty,
        PhoneNumber = emp.User?.PhoneNumber ?? string.Empty,
        Address = emp.Address,
        CreatedBy = emp.CreatedBy,
        CreatedByName = creatorName,
        OrganizationId = emp.OrganizationId
    };
}   