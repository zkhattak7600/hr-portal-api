using System.ComponentModel.DataAnnotations;

namespace EmployeeLeaveApi.DTOs;

public class RegisterDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    /// <summary>Optional portal: "Admin" or "Employee".</summary>
    public string? LoginAs { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public string? PhotoUrl { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
    public UserDto User { get; set; } = new();
}

public class MyDashboardDto
{
    public int PendingLeaves { get; set; }
    public int ApprovedLeaves { get; set; }
    public int RejectedLeaves { get; set; }
    public int LeaveDaysTakenThisYear { get; set; }
    public AttendanceSummaryDto Attendance { get; set; } = new();
    public decimal? LatestNetSalary { get; set; }
    public int? LatestPayslipMonth { get; set; }
    public int? LatestPayslipYear { get; set; }
}

public class MyLeaveCreateDto
{
    public Models.LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
}

public class ForgotPasswordDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(6, MinimumLength = 6)] public string Code { get; set; } = string.Empty;
    [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
}