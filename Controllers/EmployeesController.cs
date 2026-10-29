using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using EmployeeLeaveApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public EmployeesController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile? file)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound(new { message = "Employee not found." });
        var err = PhotoStorage.Validate(file);
        if (err != null) return BadRequest(new { message = err });
        await PhotoStorage.Save(e, file!, _env);
        await _db.SaveChangesAsync();
        return Ok(new { photoUrl = e.PhotoUrl });
    }

    [HttpDelete("{id:int}/photo")]
    public async Task<IActionResult> DeletePhoto(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound(new { message = "Employee not found." });
        PhotoStorage.Remove(e, _env);
        await _db.SaveChangesAsync();
        return Ok(new { photoUrl = (string?)null });
    }

    internal static EmployeeDto ToDto(Employee e) => new()
    {
        Id = e.Id, EmployeeCode = e.EmployeeCode, FirstName = e.FirstName, LastName = e.LastName,
        FullName = e.FirstName + " " + e.LastName, Email = e.Email, Phone = e.Phone,
        Designation = e.Designation, DepartmentId = e.DepartmentId,
        DepartmentName = e.Department != null ? e.Department.Name : "",
        JoiningDate = e.JoiningDate, BasicSalary = e.BasicSalary, Status = e.Status, UserId = e.UserId,
        PhotoUrl = e.PhotoUrl
    };

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] int? departmentId, [FromQuery] EmployeeStatus? status)
    {
        var q = _db.Employees.AsNoTracking().Include(e => e.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(e => e.FirstName.ToLower().Contains(s) || e.LastName.ToLower().Contains(s)
                || e.Email.ToLower().Contains(s) || e.EmployeeCode.ToLower().Contains(s)
                || (e.Designation != null && e.Designation.ToLower().Contains(s)));
        }
        if (departmentId.HasValue) q = q.Where(e => e.DepartmentId == departmentId);
        if (status.HasValue) q = q.Where(e => e.Status == status);
        var list = await q.OrderBy(e => e.EmployeeCode).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id)
    {
        var e = await _db.Employees.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? NotFound(new { message = "Employee not found." }) : Ok(ToDto(e));
    }

    private async Task<IActionResult?> Validate(EmployeeSaveDto dto, string email, int? id)
    {
        if (!await _db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            return BadRequest(new { message = "Department does not exist." });
        if (await _db.Employees.AnyAsync(e => e.Email == email && e.Id != id))
            return Conflict(new { message = "Employee email already exists." });
        if (dto.UserId.HasValue)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == dto.UserId))
                return BadRequest(new { message = "Linked user does not exist." });
            if (await _db.Employees.AnyAsync(e => e.UserId == dto.UserId && e.Id != id))
                return Conflict(new { message = "User is already linked to another employee." });
        }
        return null;
    }

    private async Task<string> NextCode()
    {
        var codes = await _db.Employees.Select(e => e.EmployeeCode).ToListAsync();
        var max = codes.Select(c => int.TryParse(c.Replace("EMP", ""), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"EMP{max + 1:D4}";
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(EmployeeSaveDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var err = await Validate(dto, email, null);
        if (err != null) return (ActionResult)err;
        var e = new Employee { EmployeeCode = await NextCode(), CreatedAt = DateTime.UtcNow };
        Apply(e, dto, email);
        _db.Employees.Add(e);
        await _db.SaveChangesAsync();
        await _db.Entry(e).Reference(x => x.Department).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = e.Id }, ToDto(e));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, EmployeeSaveDto dto)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound(new { message = "Employee not found." });
        var email = dto.Email.Trim().ToLowerInvariant();
        var err = await Validate(dto, email, id);
        if (err != null) return (ActionResult)err;
        Apply(e, dto, email);
        await _db.SaveChangesAsync();
        await _db.Entry(e).Reference(x => x.Department).LoadAsync();
        return Ok(ToDto(e));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound(new { message = "Employee not found." });
        _db.Employees.Remove(e); // cascades leaves, attendance, payslips
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Employee e, EmployeeSaveDto dto, string email)
    {
        e.FirstName = dto.FirstName.Trim();
        e.LastName = dto.LastName.Trim();
        e.Email = email;
        e.Phone = dto.Phone?.Trim();
        e.Designation = dto.Designation?.Trim();
        e.DepartmentId = dto.DepartmentId;
        e.JoiningDate = dto.JoiningDate.Date;
        e.BasicSalary = dto.BasicSalary;
        e.Status = dto.Status;
        e.UserId = dto.UserId;
    }
}
