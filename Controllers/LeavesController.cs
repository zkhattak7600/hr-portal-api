using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/leaves")]
public class LeavesController : ControllerBase
{
    private readonly AppDbContext _db;
    public LeavesController(AppDbContext db) => _db = db;

    private static LeaveDto ToDto(LeaveRequest l) => new()
    {
        Id = l.Id, EmployeeId = l.EmployeeId,
        EmployeeName = l.Employee != null ? l.Employee.FirstName + " " + l.Employee.LastName : "",
        EmployeeCode = l.Employee?.EmployeeCode ?? "",
        LeaveType = l.LeaveType, StartDate = l.StartDate, EndDate = l.EndDate, Days = l.Days,
        Reason = l.Reason, Status = l.Status, AdminRemarks = l.AdminRemarks, CreatedAt = l.CreatedAt
    };

    [HttpGet]
    public async Task<ActionResult<List<LeaveDto>>> GetAll([FromQuery] int? employeeId, [FromQuery] LeaveStatus? status)
    {
        var q = _db.LeaveRequests.AsNoTracking().Include(l => l.Employee).AsQueryable();
        if (employeeId.HasValue) q = q.Where(l => l.EmployeeId == employeeId);
        if (status.HasValue) q = q.Where(l => l.Status == status);
        var list = await q.OrderByDescending(l => l.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeaveDto>> Get(int id)
    {
        var l = await _db.LeaveRequests.AsNoTracking().Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        return l == null ? NotFound(new { message = "Leave request not found." }) : Ok(ToDto(l));
    }

    [HttpPost]
    public async Task<ActionResult<LeaveDto>> Create(LeaveCreateDto dto)
    {
        var start = dto.StartDate.Date;
        var end = dto.EndDate.Date;
        if (end < start) return BadRequest(new { message = "End date must be on or after start date." });
        if (!await _db.Employees.AnyAsync(e => e.Id == dto.EmployeeId))
            return BadRequest(new { message = "Employee does not exist." });
        var overlap = await _db.LeaveRequests.AnyAsync(l => l.EmployeeId == dto.EmployeeId
            && (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved)
            && l.StartDate <= end && l.EndDate >= start);
        if (overlap) return Conflict(new { message = "Overlaps an existing pending or approved leave." });

        var l = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId, LeaveType = dto.LeaveType, StartDate = start, EndDate = end,
            Days = (end - start).Days + 1, Reason = dto.Reason?.Trim(), Status = LeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.LeaveRequests.Add(l);
        await _db.SaveChangesAsync();
        await _db.Entry(l).Reference(x => x.Employee).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = l.Id }, ToDto(l));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/approve")]
    public Task<ActionResult<LeaveDto>> Approve(int id, LeaveDecisionDto? dto) => Decide(id, LeaveStatus.Approved, dto?.Remarks);

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/reject")]
    public Task<ActionResult<LeaveDto>> Reject(int id, LeaveDecisionDto? dto) => Decide(id, LeaveStatus.Rejected, dto?.Remarks);

    private async Task<ActionResult<LeaveDto>> Decide(int id, LeaveStatus status, string? remarks)
    {
        var l = await _db.LeaveRequests.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return NotFound(new { message = "Leave request not found." });
        if (l.Status != LeaveStatus.Pending)
            return BadRequest(new { message = $"Leave is already {l.Status}." });
        l.Status = status;
        l.AdminRemarks = remarks?.Trim();
        await _db.SaveChangesAsync();
        return Ok(ToDto(l));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await _db.LeaveRequests.FindAsync(id);
        if (l == null) return NotFound(new { message = "Leave request not found." });
        if (l.Status != LeaveStatus.Pending)
            return BadRequest(new { message = "Only pending leave requests can be deleted." });
        _db.LeaveRequests.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
