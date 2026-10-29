using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/payroll")]
public class PayrollController : ControllerBase
{
    private readonly AppDbContext _db;
    public PayrollController(AppDbContext db) => _db = db;

    private static PayslipDto ToDto(Payslip p) => new()
    {
        Id = p.Id, EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee != null ? p.Employee.FirstName + " " + p.Employee.LastName : "",
        EmployeeCode = p.Employee?.EmployeeCode ?? "",
        DepartmentName = p.Employee?.Department?.Name ?? "",
        Month = p.Month, Year = p.Year, BasicSalary = p.BasicSalary, Allowances = p.Allowances,
        Deductions = p.Deductions, Tax = p.Tax, NetSalary = p.NetSalary, Status = p.Status,
        GeneratedAt = p.GeneratedAt, PaidAt = p.PaidAt
    };

    private IQueryable<Payslip> Query() => _db.Payslips.Include(p => p.Employee).ThenInclude(e => e!.Department);

    /// <summary>Unpaid approved leave deduction: basic/30 per unpaid day falling in the month.</summary>
    private async Task<decimal> UnpaidLeaveDeduction(int employeeId, decimal basic, int month, int year)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var leaves = await _db.LeaveRequests.Where(l => l.EmployeeId == employeeId && l.LeaveType == LeaveType.Unpaid
            && l.Status == LeaveStatus.Approved && l.StartDate <= end && l.EndDate >= start).ToListAsync();
        var days = leaves.Sum(l => ((l.EndDate < end ? l.EndDate : end) - (l.StartDate > start ? l.StartDate : start)).Days + 1);
        return Math.Round(basic / 30m * days, 2);
    }

    private async Task<Payslip> Build(Employee e, int month, int year, decimal? basic, decimal? allowances, decimal? deductions, decimal? tax)
    {
        var b = basic ?? e.BasicSalary;
        var p = new Payslip
        {
            EmployeeId = e.Id, Month = month, Year = year, BasicSalary = b,
            Allowances = allowances ?? 0,
            Deductions = (deductions ?? 0) + await UnpaidLeaveDeduction(e.Id, b, month, year),
            Tax = tax ?? 0, Status = PayslipStatus.Draft, GeneratedAt = DateTime.UtcNow
        };
        p.Recalculate();
        return p;
    }

    [HttpGet]
    public async Task<ActionResult<List<PayslipDto>>> GetAll([FromQuery] int? month, [FromQuery] int? year, [FromQuery] int? employeeId)
    {
        var q = Query().AsNoTracking();
        if (month.HasValue) q = q.Where(p => p.Month == month);
        if (year.HasValue) q = q.Where(p => p.Year == year);
        if (employeeId.HasValue) q = q.Where(p => p.EmployeeId == employeeId);
        var list = await q.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ThenBy(p => p.EmployeeId).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PayslipDto>> Get(int id)
    {
        var p = await Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return p == null ? NotFound(new { message = "Payslip not found." }) : Ok(ToDto(p));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("generate")]
    public async Task<ActionResult<PayrollGenerateResultDto>> Generate(PayrollGenerateDto dto)
    {
        var employees = await _db.Employees.Where(e => e.Status == EmployeeStatus.Active).ToListAsync();
        var existing = await _db.Payslips.Where(p => p.Month == dto.Month && p.Year == dto.Year).Select(p => p.EmployeeId).ToListAsync();
        var created = new List<Payslip>();
        foreach (var e in employees.Where(e => !existing.Contains(e.Id)))
            created.Add(await Build(e, dto.Month, dto.Year, null, dto.Allowances, dto.Deductions, dto.Tax));
        _db.Payslips.AddRange(created);
        await _db.SaveChangesAsync();
        var ids = created.Select(c => c.Id).ToList();
        var result = await Query().AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync();
        return StatusCode(201, new PayrollGenerateResultDto
        {
            Generated = created.Count, Skipped = employees.Count - created.Count, Payslips = result.Select(ToDto).ToList()
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<PayslipDto>> Create(PayslipCreateDto dto)
    {
        var e = await _db.Employees.FindAsync(dto.EmployeeId);
        if (e == null) return BadRequest(new { message = "Employee does not exist." });
        if (await _db.Payslips.AnyAsync(p => p.EmployeeId == dto.EmployeeId && p.Month == dto.Month && p.Year == dto.Year))
            return Conflict(new { message = "Payslip already exists for this employee and period." });
        var p = await Build(e, dto.Month, dto.Year, dto.BasicSalary, dto.Allowances, dto.Deductions, dto.Tax);
        _db.Payslips.Add(p);
        await _db.SaveChangesAsync();
        var full = await Query().AsNoTracking().FirstAsync(x => x.Id == p.Id);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, ToDto(full));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PayslipDto>> Update(int id, PayslipUpdateDto dto)
    {
        var p = await Query().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound(new { message = "Payslip not found." });
        if (p.Status != PayslipStatus.Draft) return BadRequest(new { message = "Only draft payslips can be edited." });
        p.BasicSalary = dto.BasicSalary; p.Allowances = dto.Allowances; p.Deductions = dto.Deductions; p.Tax = dto.Tax;
        p.Recalculate();
        await _db.SaveChangesAsync();
        return Ok(ToDto(p));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/pay")]
    public async Task<ActionResult<PayslipDto>> Pay(int id)
    {
        var p = await Query().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound(new { message = "Payslip not found." });
        if (p.Status == PayslipStatus.Paid) return BadRequest(new { message = "Payslip is already paid." });
        p.Status = PayslipStatus.Paid;
        p.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(p));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Payslips.FindAsync(id);
        if (p == null) return NotFound(new { message = "Payslip not found." });
        if (p.Status != PayslipStatus.Draft) return BadRequest(new { message = "Only draft payslips can be deleted." });
        _db.Payslips.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
