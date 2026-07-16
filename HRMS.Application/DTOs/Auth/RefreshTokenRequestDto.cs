namespace HRMS.Application.DTOs.Auth;

/// <summary>
/// DTO used for refresh-token and logout requests. Passes the refresh token string.
/// </summary>
public class RefreshTokenRequestDto
{
    public string Token { get; set; } = string.Empty;
}
