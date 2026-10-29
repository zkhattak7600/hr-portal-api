using System.ComponentModel.DataAnnotations;
using EmployeeLeaveApi.Models;

namespace EmployeeLeaveApi.DTOs;

// ---------- Departments ----------
public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EmployeeCount { get; set; }
}

public class DepartmentSaveDto
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
}

// ---------- Employees ----------
public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Designation { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime JoiningDate { get; set; }
    public decimal BasicSalary { get; set; }
    public EmployeeStatus Status { get; set; }
    public int? UserId { get; set; }
}

public class EmployeeSaveDto
{
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(100)] public string? Designation { get; set; }
    [Range(1, int.MaxValue)] public int DepartmentId { get; set; }
    public DateTime JoiningDate { get; set; }
    [Range(0, 100000000)] public decimal BasicSalary { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public int? UserId { get; set; }
}

// ---------- Leaves ----------
public class LeaveDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; }
    public string? AdminRemarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeaveCreateDto
{
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
}

public class LeaveDecisionDto
{
    [MaxLength(500)] public string? Remarks { get; set; }
}

// ---------- Attendance ----------
public class AttendanceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? CheckIn { get; set; }   // "HH:mm:ss"
    public string? CheckOut { get; set; }  // "HH:mm:ss"
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceSaveDto
{
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public string? CheckIn { get; set; }   // "HH:mm" or "HH:mm:ss"
    public string? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class AttendanceSummaryDto
{
    public int? EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int HalfDay { get; set; }
    public int Leave { get; set; }
    public int Total { get; set; }
}

// ---------- Payroll ----------
public class PayslipDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public decimal Tax { get; set; }
    public decimal NetSalary { get; set; }
    public PayslipStatus Status { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class PayrollGenerateDto
{
    [Range(1, 12)] public int Month { get; set; }
    [Range(2000, 2100)] public int Year { get; set; }
    [Range(0, 100000000)] public decimal? Allowances { get; set; }
    [Range(0, 100000000)] public decimal? Deductions { get; set; }
    [Range(0, 100000000)] public decimal? Tax { get; set; }
}

public class PayrollGenerateResultDto
{
    public int Generated { get; set; }
    public int Skipped { get; set; }
    public List<PayslipDto> Payslips { get; set; } = new();
}

public class PayslipCreateDto
{
    [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
    [Range(1, 12)] public int Month { get; set; }
    [Range(2000, 2100)] public int Year { get; set; }
    [Range(0, 100000000)] public decimal? BasicSalary { get; set; }
    [Range(0, 100000000)] public decimal? Allowances { get; set; }
    [Range(0, 100000000)] public decimal? Deductions { get; set; }
    [Range(0, 100000000)] public decimal? Tax { get; set; }
}

public class PayslipUpdateDto
{
    [Range(0, 100000000)] public decimal BasicSalary { get; set; }
    [Range(0, 100000000)] public decimal Allowances { get; set; }
    [Range(0, 100000000)] public decimal Deductions { get; set; }
    [Range(0, 100000000)] public decimal Tax { get; set; }
}

// ---------- Dashboard ----------
public class DashboardStatsDto
{
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int TotalDepartments { get; set; }
    public int PendingLeaves { get; set; }
    public int TodayPresent { get; set; }
    public int TodayAbsent { get; set; }
    public decimal CurrentMonthPayroll { get; set; }
}

public class MyAttendanceTodayDto : AttendanceDto
{
    public double? WorkedHours { get; set; }
}


public class LeaveBalanceDto
{
    public LeaveType Type { get; set; }
    public int Entitled { get; set; }
    public int Used { get; set; }
    public int Pending { get; set; }
    public int Remaining { get; set; }
}