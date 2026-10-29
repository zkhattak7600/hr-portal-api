using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> Stats()
    {
        var today = DateTime.Today;
        // SQLite cannot SUM decimals server-side, so aggregate in memory.
        var net = await _db.Payslips.Where(p => p.Month == today.Month && p.Year == today.Year).Select(p => p.NetSalary).ToListAsync();
        return new DashboardStatsDto
        {
            TotalEmployees = await _db.Employees.CountAsync(),
            ActiveEmployees = await _db.Employees.CountAsync(e => e.Status == EmployeeStatus.Active),
            TotalDepartments = await _db.Departments.CountAsync(),
            PendingLeaves = await _db.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Pending),
            TodayPresent = await _db.Attendances.CountAsync(a => a.Date == today
                && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late || a.Status == AttendanceStatus.HalfDay)),
            TodayAbsent = await _db.Attendances.CountAsync(a => a.Date == today && a.Status == AttendanceStatus.Absent),
            CurrentMonthPayroll = net.Sum()
        };
    }
}
