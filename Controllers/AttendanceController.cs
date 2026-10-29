using System.Globalization;
using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    public AttendanceController(AppDbContext db) => _db = db;

    private static string? Fmt(TimeSpan? t) => t?.ToString(@"hh\:mm\:ss");

    private static bool TryParseTime(string? s, out TimeSpan? t)
    {
        t = null;
        if (string.IsNullOrWhiteSpace(s)) return true;
        if (TimeSpan.TryParseExact(s.Trim(), new[] { @"hh\:mm", @"hh\:mm\:ss", @"h\:mm" }, CultureInfo.InvariantCulture, out var v)
            && v >= TimeSpan.Zero && v < TimeSpan.FromDays(1)) { t = v; return true; }
        return false;
    }

    private static AttendanceDto ToDto(Attendance a) => new()
    {
        Id = a.Id, EmployeeId = a.EmployeeId,
        EmployeeName = a.Employee != null ? a.Employee.FirstName + " " + a.Employee.LastName : "",
        EmployeeCode = a.Employee?.EmployeeCode ?? "",
        Date = a.Date, CheckIn = Fmt(a.CheckIn), CheckOut = Fmt(a.CheckOut), Status = a.Status, Notes = a.Notes
    };

    [HttpGet]
    public async Task<ActionResult<List<AttendanceDto>>> GetAll([FromQuery] int? employeeId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] DateTime? date)
    {
        var q = _db.Attendances.AsNoTracking().Include(a => a.Employee).AsQueryable();
        if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId);
        if (date.HasValue) q = q.Where(a => a.Date == date.Value.Date);
        if (from.HasValue) q = q.Where(a => a.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.Date <= to.Value.Date);
        var list = await q.OrderByDescending(a => a.Date).ThenBy(a => a.EmployeeId).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AttendanceDto>> Get(int id)
    {
        var a = await _db.Attendances.AsNoTracking().Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        return a == null ? NotFound(new { message = "Attendance record not found." }) : Ok(ToDto(a));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AttendanceSummaryDto>> Summary([FromQuery] int? employeeId, [FromQuery] int? month, [FromQuery] int? year)
    {
        var now = DateTime.Today;
        var m = month ?? now.Month;
        var y = year ?? now.Year;
        if (m < 1 || m > 12) return BadRequest(new { message = "Month must be 1-12." });
        var start = new DateTime(y, m, 1);
        var end = start.AddMonths(1);
        var q = _db.Attendances.Where(a => a.Date >= start && a.Date < end);
        if (employeeId.HasValue) q = q.Where(a => a.EmployeeId == employeeId);
        var counts = await q.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        int C(AttendanceStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        return new AttendanceSummaryDto
        {
            EmployeeId = employeeId, Month = m, Year = y,
            Present = C(AttendanceStatus.Present), Absent = C(AttendanceStatus.Absent), Late = C(AttendanceStatus.Late),
            HalfDay = C(AttendanceStatus.HalfDay), Leave = C(AttendanceStatus.Leave), Total = counts.Sum(c => c.Count)
        };
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<AttendanceDto>> Create(AttendanceSaveDto dto)
    {
        if (!TryParseTime(dto.CheckIn, out var ci) || !TryParseTime(dto.CheckOut, out var co))
            return BadRequest(new { message = "Times must be in HH:mm format." });
        if (ci.HasValue && co.HasValue && co < ci) return BadRequest(new { message = "Check-out must be after check-in." });
        if (!await _db.Employees.AnyAsync(e => e.Id == dto.EmployeeId))
            return BadRequest(new { message = "Employee does not exist." });
        var date = dto.Date.Date;
        if (await _db.Attendances.AnyAsync(a => a.EmployeeId == dto.EmployeeId && a.Date == date))
            return Conflict(new { message = "Attendance already marked for this employee on this date." });
        var a = new Attendance { EmployeeId = dto.EmployeeId, Date = date, CheckIn = ci, CheckOut = co, Status = dto.Status, Notes = dto.Notes?.Trim() };
        _db.Attendances.Add(a);
        await _db.SaveChangesAsync();
        await _db.Entry(a).Reference(x => x.Employee).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = a.Id }, ToDto(a));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<AttendanceDto>> Update(int id, AttendanceSaveDto dto)
    {
        var a = await _db.Attendances.FindAsync(id);
        if (a == null) return NotFound(new { message = "Attendance record not found." });
        if (!TryParseTime(dto.CheckIn, out var ci) || !TryParseTime(dto.CheckOut, out var co))
            return BadRequest(new { message = "Times must be in HH:mm format." });
        if (ci.HasValue && co.HasValue && co < ci) return BadRequest(new { message = "Check-out must be after check-in." });
        if (!await _db.Employees.AnyAsync(e => e.Id == dto.EmployeeId))
            return BadRequest(new { message = "Employee does not exist." });
        var date = dto.Date.Date;
        if (await _db.Attendances.AnyAsync(x => x.Id != id && x.EmployeeId == dto.EmployeeId && x.Date == date))
            return Conflict(new { message = "Attendance already marked for this employee on this date." });
        a.EmployeeId = dto.EmployeeId; a.Date = date; a.CheckIn = ci; a.CheckOut = co; a.Status = dto.Status; a.Notes = dto.Notes?.Trim();
        await _db.SaveChangesAsync();
        await _db.Entry(a).Reference(x => x.Employee).LoadAsync();
        return Ok(ToDto(a));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Attendances.FindAsync(id);
        if (a == null) return NotFound(new { message = "Attendance record not found." });
        _db.Attendances.Remove(a);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
