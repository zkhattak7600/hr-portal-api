using System.Security.Claims;
using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using EmployeeLeaveApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AuthController> _log;

    public AuthController(AppDbContext db, ITokenService tokens, IWebHostEnvironment env, ILogger<AuthController> log)
    {
        _db = db;
        _tokens = tokens;
        _env = env;
        _log = log;
    }

    private const string NotLinkedMessage = "Your account is not registered in the HR system yet. Please contact HR.";

    private const string ForgotGenericMessage = "If an account exists for that email, a reset code has been generated.";

    /// <summary>Generates a 6-digit reset code valid for 15 minutes. No email service: in Development the code is returned as devCode and logged.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return Ok(new { message = ForgotGenericMessage });

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.ResetCode = BCrypt.Net.BCrypt.HashPassword(code);
        user.ResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        if (_env.IsDevelopment())
        {
            _log.LogInformation("DEV password reset code for {Email}: {Code} (valid 15 min)", email, code);
            return Ok(new { message = ForgotGenericMessage, devCode = code });
        }
        return Ok(new { message = ForgotGenericMessage });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || string.IsNullOrEmpty(user.ResetCode) || user.ResetCodeExpiry == null
            || user.ResetCodeExpiry < DateTime.UtcNow || !BCrypt.Net.BCrypt.Verify(dto.Code.Trim(), user.ResetCode))
            return BadRequest(new { message = "Invalid or expired reset code." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.ResetCode = null;
        user.ResetCodeExpiry = null;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password has been reset. You can now log in." });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email already registered." });

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Employee",
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var employeeId = await ResolveEmployeeId(user);
        return StatusCode(201, ToDto(user, employeeId));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (!string.IsNullOrWhiteSpace(dto.LoginAs))
        {
            var wanted = dto.LoginAs.Trim();
            if (!string.Equals(wanted, user.Role, StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new
                {
                    message = string.Equals(wanted, "Admin", StringComparison.OrdinalIgnoreCase)
                        ? "This account is not an Administrator account."
                        : "This account is not an Employee account."
                });
        }

        var employeeId = await ResolveEmployeeId(user);
        if (string.Equals(user.Role, "Employee", StringComparison.OrdinalIgnoreCase) && employeeId == null)
            return StatusCode(403, new { message = NotLinkedMessage });

        var (token, expiry) = _tokens.CreateToken(user, employeeId);
        return Ok(new AuthResponseDto { Token = token, Expiry = expiry, User = ToDto(user, employeeId) });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : 0;
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Unauthorized(new { message = "User not found." });
        return Ok(ToDto(user, await ResolveEmployeeId(user)));
    }

    /// <summary>Employee linked via UserId, else matching email (linked on the fly).</summary>
    private async Task<int?> ResolveEmployeeId(User user)
    {
        var linked = await _db.Employees.Where(e => e.UserId == user.Id).Select(e => (int?)e.Id).FirstOrDefaultAsync();
        if (linked.HasValue) return linked;
        var match = await _db.Employees.FirstOrDefaultAsync(e => e.Email == user.Email && e.UserId == null);
        if (match == null) return null;
        match.UserId = user.Id;
        await _db.SaveChangesAsync();
        return match.Id;
    }

    private UserDto ToDto(User u, int? employeeId) =>
        new()
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email, Role = u.Role, EmployeeId = employeeId,
            PhotoUrl = employeeId.HasValue
                ? _db.Employees.Where(e => e.Id == employeeId).Select(e => e.PhotoUrl).FirstOrDefault()
                : null
        };
}
