using System.Security.Claims;
using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using EmployeeLeaveApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

/// <summary>Self-service endpoints for the logged-in user's own employee record.</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    public MeController(AppDbContext db, IConfiguration config, IWebHostEnvironment env) { _db = db; _config = config; _env = env; }

    private static readonly object NoProfile = new { message = "No employee profile linked to this account." };

    private async Task<int?> EmployeeId()
    {
        if (int.TryParse(User.FindFirstValue(TokenService.EmployeeIdClaim), out var eid)
            && await _db.Employees.AnyAsync(e => e.Id == eid)) return eid;
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return null;
        return await _db.Employees.Where(e => e.UserId == uid).Select(e => (int?)e.Id).FirstOrDefaultAsync();
    }

    private static LeaveDto ToLeave(LeaveRequest l) => new()
    {
        Id = l.Id, EmployeeId = l.EmployeeId,
        EmployeeName = l.Employee != null ? l.Employee.FirstName + " " + l.Employee.LastName : "",
        EmployeeCode = l.Employee?.EmployeeCode ?? "",
        LeaveType = l.LeaveType, StartDate = l.StartDate, EndDate = l.EndDate, Days = l.Days,
        Reason = l.Reason, Status = l.Status, AdminRemarks = l.AdminRemarks, CreatedAt = l.CreatedAt
    };

    private static PayslipDto ToSlip(Payslip p) => new()
    {
        Id = p.Id, EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee != null ? p.Employee.FirstName + " " + p.Employee.LastName : "",
        EmployeeCode = p.Employee?.EmployeeCode ?? "",
        DepartmentName = p.Employee?.Department?.Name ?? "",
        Month = p.Month, Year = p.Year, BasicSalary = p.BasicSalary, Allowances = p.Allowances,
        Deductions = p.Deductions, Tax = p.Tax, NetSalary = p.NetSalary, Status = p.Status,
        GeneratedAt = p.GeneratedAt, PaidAt = p.PaidAt
    };

    [HttpGet("profile")]
    public async Task<ActionResult<EmployeeDto>> Profile()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var e = await _db.Employees.AsNoTracking().Include(x => x.Department).FirstAsync(x => x.Id == id);
        return EmployeesController.ToDto(e);
    }

    [HttpPost("photo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile? file)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var err = PhotoStorage.Validate(file);
        if (err != null) return BadRequest(new { message = err });
        var e = await _db.Employees.FirstAsync(x => x.Id == id);
        await PhotoStorage.Save(e, file!, _env);
        await _db.SaveChangesAsync();
        return Ok(new { photoUrl = e.PhotoUrl });
    }

    [HttpDelete("photo")]
    public async Task<IActionResult> DeletePhoto()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var e = await _db.Employees.FirstAsync(x => x.Id == id);
        PhotoStorage.Remove(e, _env);
        await _db.SaveChangesAsync();
        return Ok(new { photoUrl = (string?)null });
    }

    [HttpGet("leaves")]
    public async Task<ActionResult<List<LeaveDto>>> Leaves()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var list = await _db.LeaveRequests.AsNoTracking().Include(l => l.Employee)
            .Where(l => l.EmployeeId == id).OrderByDescending(l => l.CreatedAt).ToListAsync();
        return list.Select(ToLeave).ToList();
    }

    [HttpPost("leaves")]
    public async Task<ActionResult<LeaveDto>> ApplyLeave(MyLeaveCreateDto dto)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var start = dto.StartDate.Date;
        var end = dto.EndDate.Date;
        if (end < start) return BadRequest(new { message = "End date must be on or after start date." });
        var overlap = await _db.LeaveRequests.AnyAsync(l => l.EmployeeId == id
            && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
            && l.StartDate <= end && l.EndDate >= start);
        if (overlap) return Conflict(new { message = "Overlaps an existing pending or approved leave." });
        var l = new LeaveRequest
        {
            EmployeeId = id.Value, LeaveType = dto.LeaveType, StartDate = start, EndDate = end,
            Days = (end - start).Days + 1, Reason = dto.Reason?.Trim(), Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.LeaveRequests.Add(l);
        await _db.SaveChangesAsync();
        await _db.Entry(l).Reference(x => x.Employee).LoadAsync();
        return StatusCode(201, ToLeave(l));
    }

    [HttpDelete("leaves/{leaveId:int}")]
    public async Task<IActionResult> CancelLeave(int leaveId)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var l = await _db.LeaveRequests.FirstOrDefaultAsync(x => x.Id == leaveId && x.EmployeeId == id);
        if (l == null) return NotFound(new { message = "Leave request not found." });
        if (l.Status != LeaveStatus.Pending)
            return BadRequest(new { message = "Only pending leave requests can be cancelled." });
        _db.LeaveRequests.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Per leave type: yearly entitlement (config LeaveEntitlements), approved days used this year, pending days, remaining.</summary>
    [HttpGet("leave-balance")]
    public async Task<ActionResult<List<LeaveBalanceDto>>> LeaveBalance([FromQuery] int? year)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var y = year ?? DateTime.Today.Year;
        var from = new DateTime(y, 1, 1); var to = new DateTime(y, 12, 31);
        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == id && l.StartDate <= to && l.EndDate >= from
                && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending))
            .ToListAsync();
        int DaysInYear(LeaveRequest l)
        {
            var s = l.StartDate.Date < from ? from : l.StartDate.Date;
            var e = l.EndDate.Date > to ? to : l.EndDate.Date;
            return e < s ? 0 : (e - s).Days + 1;
        }
        var result = new List<LeaveBalanceDto>();
        foreach (var t in Enum.GetValues<LeaveType>())
        {
            var entitled = int.TryParse(_config[$"LeaveEntitlements:{t}"], out var n) ? Math.Max(0, n) : 0;
            var used = leaves.Where(l => l.LeaveType == t && l.Status == LeaveStatus.Approved).Sum(DaysInYear);
            var pending = leaves.Where(l => l.LeaveType == t && l.Status == LeaveStatus.Pending).Sum(DaysInYear);
            result.Add(new LeaveBalanceDto { Type = t, Entitled = entitled, Used = used, Pending = pending, Remaining = Math.Max(0, entitled - used) });
        }
        return result;
    }

    [HttpGet("attendance")]
    public async Task<ActionResult<List<AttendanceDto>>> Attendance([FromQuery] int? month, [FromQuery] int? year)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var m = month ?? DateTime.Today.Month; var y = year ?? DateTime.Today.Year;
        if (m < 1 || m > 12) return BadRequest(new { message = "Month must be 1-12." });
        var start = new DateTime(y, m, 1); var end = start.AddMonths(1);
        var list = await _db.Attendances.AsNoTracking().Include(a => a.Employee)
            .Where(a => a.EmployeeId == id && a.Date >= start && a.Date < end).OrderByDescending(a => a.Date).ToListAsync();
        return list.Select(a => new AttendanceDto
        {
            Id = a.Id, EmployeeId = a.EmployeeId,
            EmployeeName = a.Employee != null ? a.Employee.FirstName + " " + a.Employee.LastName : "",
            EmployeeCode = a.Employee?.EmployeeCode ?? "", Date = a.Date,
            CheckIn = a.CheckIn?.ToString(@"hh\:mm\:ss"), CheckOut = a.CheckOut?.ToString(@"hh\:mm\:ss"),
            Status = a.Status, Notes = a.Notes
        }).ToList();
    }

    private async Task<AttendanceSummaryDto> Summary(int id, int m, int y)
    {
        var start = new DateTime(y, m, 1); var end = start.AddMonths(1);
        var counts = await _db.Attendances.Where(a => a.EmployeeId == id && a.Date >= start && a.Date < end)
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        int C(AttendanceStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        return new AttendanceSummaryDto
        {
            EmployeeId = id, Month = m, Year = y,
            Present = C(AttendanceStatus.Present), Absent = C(AttendanceStatus.Absent), Late = C(AttendanceStatus.Late),
            HalfDay = C(AttendanceStatus.HalfDay), Leave = C(AttendanceStatus.Leave), Total = counts.Sum(c => c.Count)
        };
    }

    [HttpGet("attendance/summary")]
    public async Task<ActionResult<AttendanceSummaryDto>> AttendanceSummary([FromQuery] int? month, [FromQuery] int? year)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var m = month ?? DateTime.Today.Month; var y = year ?? DateTime.Today.Year;
        if (m < 1 || m > 12) return BadRequest(new { message = "Month must be 1-12." });
        return await Summary(id.Value, m, y);
    }

    [HttpGet("payslips")]
    public async Task<ActionResult<List<PayslipDto>>> Payslips()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var list = await _db.Payslips.AsNoTracking().Include(p => p.Employee).ThenInclude(e => e!.Department)
            .Where(p => p.EmployeeId == id).OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync();
        return list.Select(ToSlip).ToList();
    }

    [HttpGet("payslips/{slipId:int}")]
    public async Task<ActionResult<PayslipDto>> Payslip(int slipId)
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var p = await _db.Payslips.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e!.Department)
            .FirstOrDefaultAsync(x => x.Id == slipId && x.EmployeeId == id);
        return p == null ? NotFound(new { message = "Payslip not found." }) : Ok(ToSlip(p));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<MyDashboardDto>> Dashboard()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var today = DateTime.Today;
        var leaves = await _db.LeaveRequests.AsNoTracking().Where(l => l.EmployeeId == id).ToListAsync();
        var yStart = new DateTime(today.Year, 1, 1); var yEnd = new DateTime(today.Year, 12, 31);
        var taken = leaves.Where(l => l.Status == LeaveStatus.Approved && l.StartDate <= yEnd && l.EndDate >= yStart)
            .Sum(l => ((l.EndDate < yEnd ? l.EndDate : yEnd) - (l.StartDate > yStart ? l.StartDate : yStart)).Days + 1);
        var latest = await _db.Payslips.AsNoTracking().Where(p => p.EmployeeId == id)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).FirstOrDefaultAsync();
        return new MyDashboardDto
        {
            PendingLeaves = leaves.Count(l => l.Status == LeaveStatus.Pending),
            ApprovedLeaves = leaves.Count(l => l.Status == LeaveStatus.Approved),
            RejectedLeaves = leaves.Count(l => l.Status == LeaveStatus.Rejected),
            LeaveDaysTakenThisYear = taken,
            Attendance = await Summary(id.Value, today.Month, today.Year),
            LatestNetSalary = latest?.NetSalary, LatestPayslipMonth = latest?.Month, LatestPayslipYear = latest?.Year
        };
    }

    // ---------- Self check-in / check-out ----------
    private TimeSpan LateAfter => TimeSpan.TryParse(_config["Attendance:LateAfter"], out var t) ? t : new TimeSpan(9, 15, 0);
    private double HalfDayHours => double.TryParse(_config["Attendance:HalfDayHours"], System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : 4;

    private static double? Worked(Attendance a)
    {
        if (a.CheckIn == null) return null;
        var end = a.CheckOut ?? (a.Date.Date == DateTime.Today ? DateTime.Now.TimeOfDay : a.CheckIn.Value);
        var hrs = (end - a.CheckIn.Value).TotalHours;
        return Math.Round(Math.Max(0, hrs), 2);
    }

    private static MyAttendanceTodayDto ToToday(Attendance a) => new()
    {
        Id = a.Id, EmployeeId = a.EmployeeId,
        EmployeeName = a.Employee != null ? a.Employee.FirstName + " " + a.Employee.LastName : "",
        EmployeeCode = a.Employee?.EmployeeCode ?? "", Date = a.Date,
        CheckIn = a.CheckIn?.ToString(@"hh\:mm\:ss"), CheckOut = a.CheckOut?.ToString(@"hh\:mm\:ss"),
        Status = a.Status, Notes = a.Notes, WorkedHours = Worked(a)
    };

    [HttpGet("attendance/today")]
    public async Task<ActionResult<MyAttendanceTodayDto?>> Today()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var today = DateTime.Today;
        var a = await _db.Attendances.AsNoTracking().Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.EmployeeId == id && x.Date == today);
        return Ok(a == null ? null : ToToday(a));
    }

    [HttpPost("attendance/check-in")]
    public async Task<ActionResult<MyAttendanceTodayDto>> CheckIn()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var now = DateTime.Now; var today = now.Date;
        var time = new TimeSpan(now.Hour, now.Minute, now.Second);
        var a = await _db.Attendances.Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.EmployeeId == id && x.Date == today);
        if (a?.CheckIn != null) return Conflict(new { message = "Already checked in today." });
        if (a == null)
        {
            a = new Attendance { EmployeeId = id.Value, Date = today };
            _db.Attendances.Add(a);
        }
        a.CheckIn = time;
        a.CheckOut = null;
        a.Status = time > LateAfter ? AttendanceStatus.Late : AttendanceStatus.Present;
        await _db.SaveChangesAsync();
        if (a.Employee == null) await _db.Entry(a).Reference(x => x.Employee).LoadAsync();
        return Ok(ToToday(a));
    }

    [HttpPost("attendance/check-out")]
    public async Task<ActionResult<MyAttendanceTodayDto>> CheckOut()
    {
        var id = await EmployeeId();
        if (id == null) return NotFound(NoProfile);
        var now = DateTime.Now; var today = now.Date;
        var a = await _db.Attendances.Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.EmployeeId == id && x.Date == today);
        if (a?.CheckIn == null) return BadRequest(new { message = "You have not checked in today." });
        if (a.CheckOut != null) return Conflict(new { message = "Already checked out today." });
        a.CheckOut = new TimeSpan(now.Hour, now.Minute, now.Second);
        if ((a.CheckOut.Value - a.CheckIn.Value).TotalHours < HalfDayHours) a.Status = AttendanceStatus.HalfDay;
        await _db.SaveChangesAsync();
        return Ok(ToToday(a));
    }
}
