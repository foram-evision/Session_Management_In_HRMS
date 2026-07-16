using System.Security.Claims;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers;

[ApiController]
// 1. Explicitly setting the route to "api/users" so it reflects globally across all roles
[Route("api/users")]
[Authorize(Roles = $"{AppConstants.Roles.Admin},{AppConstants.Roles.HR}")]
// 2. Class name renamed to match the filename exactly
public class UsersController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    // 3. Constructor name updated to match the class name
    public UsersController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<UserListItemDto>.Failure("User ID claim not found."));

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _employeeService.CreateUserAsync(dto, userId, role);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? roleFilter = null)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<List<UserListItemDto>>.Failure("User ID claim not found."));

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _employeeService.GetUsersAsync(userId, role, roleFilter);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<UserListItemDto>.Failure("User ID claim not found."));

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _employeeService.GetUserByIdAsync(id, userId, role);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<UserListItemDto>.Failure("User ID claim not found."));

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _employeeService.UpdateUserAsync(id, dto, userId, role);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<bool>.Failure("User ID claim not found."));

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _employeeService.DeleteUserAsync(id, userId, role);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }
}