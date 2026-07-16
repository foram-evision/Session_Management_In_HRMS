using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HRMS.Application.Services;

/// <summary>
/// Enterprise authentication service.
///
/// Security features implemented:
///   1. Refresh token SHA-256 hashing  — tokens never stored in plain text
///   2. Multi-device session support   — one session per login, no cross-device revocation on login
///   3. Sliding expiry                 — session lifetime extended on each successful refresh
///   4. Absolute expiry                — forces re-login after a configurable maximum (default 90 days)
///   5. Refresh token rotation         — old token revoked and linked to new token on every refresh
///   6. Reuse detection                — presenting a revoked token triggers full session wipe + audit
///   7. Audit logging                  — every auth event written to AuthAuditLogs
///   8. Device / IP metadata           — captured from HTTP context, stored per session
///   9. SessionId JWT claim ("sid")    — ties access token to a specific revocable session
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly IRefreshTokenRepository       _refreshTokenRepository;
    private readonly IAuthAuditRepository          _auditRepository;
    private readonly IConfiguration                _configuration;
    private readonly IHttpContextAccessor          _httpContextAccessor;
    private readonly ILogger<AuthService>          _logger;

    public AuthService(
        UserManager<ApplicationUser>  userManager,
        IRefreshTokenRepository       refreshTokenRepository,
        IAuthAuditRepository          auditRepository,
        IConfiguration                configuration,
        IHttpContextAccessor          httpContextAccessor,
        ILogger<AuthService>          logger)
    {
        _userManager            = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _auditRepository        = auditRepository;
        _configuration          = configuration;
        _httpContextAccessor    = httpContextAccessor;
        _logger                 = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC AUTH OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    // ─── Register ─────────────────────────────────────────────────────────────

    /* Replaced method: RegisterAsync */
    public async Task<ApiResponse<bool>> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
            return ApiResponse<bool>.Failure(AppConstants.Messages.UserAlreadyExists);

        var user = new ApplicationUser
        {
            // ApplicationUser.Id is a Guid, so assign a Guid directly.
            Id                 = Guid.NewGuid(),
            FullName           = dto.FullName,
            Email              = dto.Email,
            UserName           = dto.Email,
            NormalizedEmail    = dto.Email.ToUpperInvariant(),
            NormalizedUserName = dto.Email.ToUpperInvariant(),
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return ApiResponse<bool>.Failure(
                "Registration failed.",
                result.Errors.Select(e => e.Description).ToList());

        var roleResult = await _userManager.AddToRoleAsync(user, AppConstants.Roles.Admin);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return ApiResponse<bool>.Failure(
                "Registration failed to assign Admin role.",
                roleResult.Errors.Select(e => e.Description).ToList());
        }

        return ApiResponse<bool>.SuccessResult(true, "Registration successful. Please log in.");
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticates the user and creates a new independent session.
    /// Does NOT revoke existing sessions — supports simultaneous multi-device logins.
    /// Records a Login or LoginFailed audit event.
    /// </summary>
    public async Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto dto
)
    {
        var ipAddress = GetIpAddress();
        var userAgent = GetUserAgent();
        var (browser, device, os) = ParseUserAgent(userAgent);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            Guid? failedUserId = user != null ? user.Id : (Guid?)null;
            await WriteAuditAsync(
                failedUserId,
                AppConstants.AuditEvents.LoginFailed,
                ipAddress, userAgent, browser, device,
                $"Email not registered: {dto.Email}");
            // Return the same message as wrong password — never reveal which field is wrong
            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.InvalidCredentials);
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
        {
            Guid? failedUserId = user.Id;
            await WriteAuditAsync(
                failedUserId,
                AppConstants.AuditEvents.LoginFailed, ipAddress, userAgent, browser, device,
                "Invalid password attempt.");
            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.InvalidCredentials);
        }

        // Update last login timestamp
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        // Create a new independent session (no existing sessions are touched)
        (string rawToken, RefreshToken session) = await CreateSessionAsync(
            user.Id, ipAddress, browser, device, os);
        var accessToken = GenerateJwtToken(user, roles, user.Id);

        await WriteAuditAsync(
            user.Id,
            AppConstants.AuditEvents.Login, ipAddress, userAgent, browser, device,
            $"New session created. SessionId={session.SessionId}");

        return ApiResponse<TokenResponseDto>.SuccessResult(new TokenResponseDto
        {
            AccessToken    = accessToken,
            RefreshToken   = rawToken,          // Raw token returned to client ONCE, then gone from server
            SessionId      = session.SessionId,
            ExpiresAt      = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
            FullName       = user.FullName,
            Email          = user.Email!,
            UserId         = user.Id,
            OrganizationId = user.OrganizationId,
            Roles          = roles
        }, "Login successful.");
    }

    // ─── Refresh Token ────────────────────────────────────────────────────────

    /// <summary>
    /// Rotates the refresh token with the following security checks (in order):
    ///   1. Hash incoming token → look up by hash
    ///   2. If NOT FOUND          → reject (invalid token)
    ///   3. If REVOKED            → token reuse detected → revoke ALL sessions + audit
    ///   4. If ABSOLUTELY EXPIRED → revoke this session, return 401
    ///   5. If SLIDING EXPIRED    → revoke this session, return 401
    ///   6. Rotate: revoke old token (linked to new hash), issue new token, extend sliding expiry
    /// </summary>
    public async Task<ApiResponse<TokenResponseDto>> RefreshTokenAsync(string token)
    {
        var ipAddress = GetIpAddress();
        var userAgent = GetUserAgent();
        var (browser, device, os) = ParseUserAgent(userAgent);

        var tokenHash = HashRefreshToken(token);
        var session   = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        // ── Not found ────────────────────────────────────────────────────────
        if (session == null)
            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.InvalidToken);

        // ── Reuse detection (token was already rotated/revoked) ───────────────
        if (session.IsRevoked)
        {
            _logger.LogWarning("Token reuse detected! UserId={UserId}, SessionId={SessionId}, IP={IP}",
                session.UserId, session.SessionId, ipAddress);

            // Revoke ALL active sessions for this user — potential token theft
            await _refreshTokenRepository.RevokeAllUserSessionsAsync(
                session.UserId,
                ipAddress,
                AppConstants.AuditEvents.TokenReuseDetected);

            await WriteAuditAsync(session.UserId, AppConstants.AuditEvents.TokenReuseDetected, ipAddress, userAgent, browser, device,
                $"Revoked token reused. SessionId={session.SessionId}. All sessions revoked as security measure.");

            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.TokenReuseDetected);
        }

        // ── Absolute expiry ───────────────────────────────────────────────────
        if (session.IsAbsolutelyExpired)
        {
            session.Revoked      = DateTime.UtcNow;
            session.RevokedByIp  = ipAddress;
            session.RevokeReason = "AbsoluteExpiry";
            await _refreshTokenRepository.UpdateAsync(session);

            await WriteAuditAsync(session.UserId, AppConstants.AuditEvents.SessionExpired, ipAddress, userAgent, browser, device,
                $"Absolute session lifetime exceeded. SessionId={session.SessionId}");

            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.SessionAbsoluteExpired);
        }

        // ── Sliding expiry ────────────────────────────────────────────────────
        if (session.IsExpired)
        {
            session.Revoked      = DateTime.UtcNow;
            session.RevokedByIp  = ipAddress;
            session.RevokeReason = "SlidingExpiry";
            await _refreshTokenRepository.UpdateAsync(session);

            await WriteAuditAsync(session.UserId, AppConstants.AuditEvents.SessionExpired, ipAddress, userAgent, browser, device,
                $"Sliding expiry reached. SessionId={session.SessionId}");

            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.InvalidToken);
        }

        // ── User still exists? ─────────────────────────────────────────────────
        var user = await _userManager.FindByIdAsync(session.UserId.ToString());
        if (user == null)
        {
            session.Revoked      = DateTime.UtcNow;
            session.RevokedByIp  = ipAddress;
            session.RevokeReason = "UserDeleted";
            await _refreshTokenRepository.UpdateAsync(session);
            return ApiResponse<TokenResponseDto>.Failure(AppConstants.Messages.UserNotFound);
        }

        // ── Rotate: generate new token, link old → new, extend sliding window ─
        var newRawToken = GenerateRawRefreshToken();
        var newHash     = HashRefreshToken(newRawToken);

        // Revoke old session and link it to the replacement
        session.Revoked             = DateTime.UtcNow;
        session.RevokedByIp         = ipAddress;
        session.LastUsed            = DateTime.UtcNow;
        session.ReplacedByTokenHash = newHash;
        session.RevokeReason        = "Rotated";
        await _refreshTokenRepository.UpdateAsync(session);

        // Build new session — inherit device metadata, inherit absolute expiry ceiling, extend sliding window
        var newSession = BuildSessionRecord(
            userId:          user.Id, // <-- FIXED: use user.Id directly
            tokenHash:       newHash,
            ipAddress:       ipAddress,
            browser:         session.Browser   ?? browser,
            device:          session.Device    ?? device,
            operatingSystem: session.OperatingSystem ?? os,
            absoluteExpiry:  session.AbsoluteExpiry); // << NEVER extended — same as original login

        await _refreshTokenRepository.AddAsync(newSession);

        var roles         = (await _userManager.GetRolesAsync(user)).ToList();
        var newAccessToken = GenerateJwtToken(user, roles, newSession.SessionId);

        await WriteAuditAsync(
            user.Id,
            AppConstants.AuditEvents.TokenRefreshed, ipAddress, userAgent, browser, device,
            $"Rotated. Old={session.SessionId}, New={newSession.SessionId}");

        return ApiResponse<TokenResponseDto>.SuccessResult(new TokenResponseDto
        {
            AccessToken    = newAccessToken,
            RefreshToken   = newRawToken,
            SessionId      = newSession.SessionId,
            ExpiresAt      = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
            FullName       = user.FullName,
            Email          = user.Email!,
            UserId         = user.Id, // <-- FIXED: use user.Id directly
            OrganizationId = user.OrganizationId,
            Roles          = roles
        }, "Token refreshed successfully.");
    }

    // ─── Logout (current device) ───────────────────────────────────────────────

    /// <summary>
    /// Revokes only the session identified by the submitted raw refresh token.
    /// Other sessions (other devices) remain active.
    /// </summary>
    public async Task<ApiResponse<bool>> LogoutAsync(string token)
    {
        var ipAddress = GetIpAddress();
        var userAgent = GetUserAgent();
        var (browser, device, _) = ParseUserAgent(userAgent);

        var tokenHash = HashRefreshToken(token);
        var session   = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (session == null)
            return ApiResponse<bool>.Failure(AppConstants.Messages.InvalidToken);

        if (!session.IsActive)
            return ApiResponse<bool>.SuccessResult(true, "Already logged out.");

        session.Revoked      = DateTime.UtcNow;
        session.RevokedByIp  = ipAddress;
        session.RevokeReason = "Logout";
        await _refreshTokenRepository.UpdateAsync(session);

        await WriteAuditAsync(session.UserId, AppConstants.AuditEvents.Logout, ipAddress, userAgent, browser, device,
            $"Single device logout. SessionId={session.SessionId}");

        return ApiResponse<bool>.SuccessResult(true, "Logged out successfully.");
    }

    // ─── Logout All Devices ────────────────────────────────────────────────────

    /// <summary>
    /// Revokes every active session for the given user in a single database call.
    /// All devices (phone, tablet, laptop, etc.) will be forced to re-authenticate.
    /// </summary>
    public async Task<ApiResponse<bool>> LogoutAllAsync(Guid userId)
    {
        var ipAddress = GetIpAddress();
        var userAgent = GetUserAgent();
        var (browser, device, _) = ParseUserAgent(userAgent);

        await _refreshTokenRepository.RevokeAllUserSessionsAsync(userId, ipAddress, "LogoutAll");

        await WriteAuditAsync(userId, AppConstants.AuditEvents.LogoutAll, ipAddress, userAgent, browser, device,
            "All sessions revoked by user request.");

        return ApiResponse<bool>.SuccessResult(true, "Logged out from all devices.");
    }

    // ─── Get Active Sessions ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a list of all currently active sessions for the user.
    /// The session matching <paramref name="currentSessionId"/> is marked IsCurrentSession = true
    /// so the client can indicate "this device" in the UI.
    /// </summary>
    public async Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId, Guid currentSessionId)
    {
        var sessions = await _refreshTokenRepository.GetActiveSessionsByUserIdAsync(userId);

        var dtos = sessions.Select(s => new ActiveSessionDto
        {
                SessionId       = s.SessionId,
                Browser         = s.Browser,
            Device          = s.Device,
            OperatingSystem = s.OperatingSystem,
            CreatedAt       = s.Created,
            LastUsed        = s.LastUsed,
            IpAddress       = s.CreatedByIp,
            IsCurrentSession = s.SessionId == currentSessionId,
            Status          = "Active"
        }).ToList();

        return ApiResponse<List<ActiveSessionDto>>.SuccessResult(dtos, $"{dtos.Count} active session(s) found.");
    }

    // ─── Revoke Specific Session ───────────────────────────────────────────────

    /// <summary>
    /// Revokes a single session identified by <paramref name="sessionId"/>.
    /// Only the session's owner (<paramref name="requestingUserId"/>) may perform this action.
    /// This allows a user to remotely sign out from a specific device.
    /// </summary>
    public async Task<ApiResponse<bool>> RevokeSessionAsync(Guid sessionId, Guid requestingUserId)
    {
        var ipAddress = GetIpAddress();
        var userAgent = GetUserAgent();
        var (browser, device, _) = ParseUserAgent(userAgent);

        var session = await _refreshTokenRepository.GetBySessionIdAsync(sessionId);

        if (session == null)
            return ApiResponse<bool>.Failure(AppConstants.Messages.SessionNotFound);

        // Ownership check — prevent cross-user session manipulation
        if (session.UserId != requestingUserId)
            return ApiResponse<bool>.Failure(AppConstants.Messages.SessionUnauthorized);

        if (!session.IsActive)
            return ApiResponse<bool>.Failure(AppConstants.Messages.SessionAlreadyRevoked);

        session.Revoked      = DateTime.UtcNow;
        session.RevokedByIp  = ipAddress;
        session.RevokeReason = "ManualRevoke";
        await _refreshTokenRepository.UpdateAsync(session);

        await WriteAuditAsync(requestingUserId, AppConstants.AuditEvents.SessionRevoked, ipAddress, userAgent, browser, device,
            $"Session manually revoked. SessionId={sessionId}");

        return ApiResponse<bool>.SuccessResult(true, "Session revoked successfully.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    // ─── Token Generation ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates a signed JWT access token.
    /// Claims: Sub, Jti, Email (two forms), NameIdentifier, Name, fullName, organizationId, roles, sid (sessionId).
    /// </summary>
    private string GenerateJwtToken(ApplicationUser user, List<string> roles, Guid sessionId)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey   = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json.");

        var key                = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier,     user.Id.ToString()),
            new Claim(ClaimTypes.Name,               user.FullName),
            new Claim(ClaimTypes.Email,              user.Email ?? string.Empty),
            new Claim("fullName",                    user.FullName),
            new Claim("organizationId",              user.OrganizationId?.ToString() ?? string.Empty),
            // "sid" links this JWT to a specific session — enables per-session revocation
            new Claim("sid",                         sessionId.ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var tokenDescriptor = new JwtSecurityToken(
            issuer:             jwtSettings["Issuer"] ?? "HRMS.API",
            audience:           jwtSettings["Audience"] ?? "HRMS.Client",
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    /// <summary>
    /// Creates a new session: generates a cryptographically secure raw token,
    /// hashes it, builds the session record, persists it, and returns both the
    /// raw token (for the client) and the session entity.
    /// </summary>
    private async Task<(string RawToken, RefreshToken Session)> CreateSessionAsync(
        Guid userId, string ipAddress, string browser, string device, string os)
    {
        var rawToken = GenerateRawRefreshToken();
        var hash     = HashRefreshToken(rawToken);

        var session = BuildSessionRecord(
            userId:          userId,
            tokenHash:       hash,
            ipAddress:       ipAddress,
            browser:         browser,
            device:          device,
            operatingSystem: os,
            absoluteExpiry:  DateTime.UtcNow.AddDays(GetAbsoluteExpiryDays())); // set once at login

        await _refreshTokenRepository.AddAsync(session);
        return (rawToken, session);
    }

    /// <summary>
    /// Constructs a RefreshToken session record.
    /// Sliding expiry is always computed fresh (from now + SlidingExpiryDays).
    /// Absolute expiry is passed in as a parameter — it is NEVER extended.
    /// </summary>
    private RefreshToken BuildSessionRecord(
        Guid userId, string tokenHash, string ipAddress,
        string? browser, string? device, string? operatingSystem, DateTime absoluteExpiry)
    {
        return new RefreshToken
        {
            Id              = Guid.NewGuid(),
            SessionId       = Guid.NewGuid(),
            TokenHash       = tokenHash,
            Created         = DateTime.UtcNow,
            Expires         = DateTime.UtcNow.AddDays(GetSlidingExpiryDays()),  // sliding window
            AbsoluteExpiry  = absoluteExpiry,                                    // hard ceiling
            CreatedByIp     = ipAddress,
            Browser         = browser,
            Device          = device,
            OperatingSystem = operatingSystem,
            UserId          = userId
        };
    }

    /// <summary>
    /// Generates a 64-byte cryptographically random token encoded as Base64.
    /// This raw value is returned to the client and NEVER stored server-side.
    /// </summary>
    private static string GenerateRawRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    // ─── Token Hashing ────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the SHA-256 hash of a raw refresh token.
    /// Returns a lowercase hex string (64 characters).
    /// </summary>
    public static string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies a raw token against a stored hash without timing-side-channel issues.
    /// (Hex comparison is constant-time for equal-length strings.)
    /// </summary>
    public static bool VerifyRefreshTokenHash(string rawToken, string storedHash)
        => string.Equals(HashRefreshToken(rawToken), storedHash, StringComparison.OrdinalIgnoreCase);

    // ─── HTTP Context Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Extracts the client IP address. Handles proxies / load balancers
    /// by checking X-Forwarded-For before falling back to the direct connection IP.
    /// </summary>
    private string GetIpAddress()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return "Unknown";

        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',').First().Trim();

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    /// <summary>Returns the raw User-Agent header string.</summary>
    private string GetUserAgent()
        => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

    // ─── User-Agent Parsing ───────────────────────────────────────────────────

    /// <summary>
    /// Lightweight User-Agent parser for browser, device type, and OS detection.
    /// No external package required — string inspection is sufficient for session metadata.
    /// Returns ("Unknown", "Desktop", "Unknown") when the header is empty.
    /// </summary>
    private static (string Browser, string Device, string OS) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return ("Unknown", "Desktop", "Unknown");

        var ua = userAgent.ToLowerInvariant();

        // Browser detection (order matters — Edge is Chromium-based, must come before Chrome)
        string browser;
        if      (ua.Contains("edg/"))                              browser = "Edge";
        else if (ua.Contains("opr/")  || ua.Contains("opera"))    browser = "Opera";
        else if (ua.Contains("chrome/") && !ua.Contains("edg/"))  browser = "Chrome";
        else if (ua.Contains("firefox/"))                          browser = "Firefox";
        else if (ua.Contains("safari/") && !ua.Contains("chrome")) browser = "Safari";
        else                                                        browser = "Other";

        // Operating system
        string os;
        if      (ua.Contains("iphone") || ua.Contains("ipad"))     os = "iOS";
        else if (ua.Contains("android"))                            os = "Android";
        else if (ua.Contains("windows nt"))                         os = "Windows";
        else if (ua.Contains("mac os x") || ua.Contains("macintosh")) os = "macOS";
        else if (ua.Contains("linux"))                              os = "Linux";
        else                                                        os = "Unknown";

        // Device type
        string device;
        if      (ua.Contains("tablet") || ua.Contains("ipad"))     device = "Tablet";
        else if (ua.Contains("mobile") || ua.Contains("iphone") || ua.Contains("android"))
                                                                    device = "Mobile";
        else                                                        device = "Desktop";

        return (browser, device, os);
    }

    // ─── Audit Helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fire-and-forget audit write. Never throws — failures are logged inside the repository.
    /// </summary>
    private Task WriteAuditAsync(
        Guid? userId, string eventName, string ipAddress, string userAgent,
        string browser, string device, string? details = null)
    {
        var log = new AuthAuditLog
        {
            Id          = Guid.NewGuid(),
            UserId      = userId,
            Event       = eventName,
            OccurredAt  = DateTime.UtcNow,
            IpAddress   = ipAddress,
            UserAgent   = userAgent.Length > 512 ? userAgent[..512] : userAgent,
            Browser     = browser,
            Device      = device,
            Details     = details
        };
        return _auditRepository.AddAsync(log);
    }

    // ─── Configuration Readers ────────────────────────────────────────────────

    private int GetJwtExpiryMinutes()
    {
        var v = _configuration["JwtSettings:ExpiryMinutes"];
        return int.TryParse(v, out var n) ? n : 60;
    }

    private int GetSlidingExpiryDays()
    {
        var v = _configuration["JwtSettings:SlidingExpiryDays"];
        return int.TryParse(v, out var n) ? n : 30;
    }

    private int GetAbsoluteExpiryDays()
    {
        var v = _configuration["JwtSettings:AbsoluteExpiryDays"];
        return int.TryParse(v, out var n) ? n : 90;
    }
}
