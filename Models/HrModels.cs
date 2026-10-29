namespace EmployeeLeaveApi.Models;

public enum EmployeeStatus { Active, Inactive }
public enum LeaveType { Annual, Sick, Casual, Unpaid }
public enum LeaveStatus { Pending, Approved, Rejected }
public enum AttendanceStatus { Present, Absent, Late, HalfDay, Leave }
public enum PayslipStatus { Draft, Paid }

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Employee> Employees { get; set; } = new();
}

public class Employee
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Designation { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public DateTime JoiningDate { get; set; }
    public decimal BasicSalary { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? PhotoUrl { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? AdminRemarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Attendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class Payslip
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public decimal Tax { get; set; }
    public decimal NetSalary { get; set; }
    public PayslipStatus Status { get; set; } = PayslipStatus.Draft;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public void Recalculate() => NetSalary = BasicSalary + Allowances - Deductions - Tax;
}
