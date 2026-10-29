namespace EmployeeLeaveApi.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee"; // "Admin" or "Employee"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Hashed 6-digit password reset code (forgot-password flow).</summary>
    public string? ResetCode { get; set; }
    public DateTime? ResetCodeExpiry { get; set; }
}
