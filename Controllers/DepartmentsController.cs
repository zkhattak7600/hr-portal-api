using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.DTOs;
using EmployeeLeaveApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeLeaveApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public DepartmentsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<DepartmentDto>>> GetAll() =>
        await _db.Departments.OrderBy(d => d.Name).Select(d => new DepartmentDto
        {
            Id = d.Id, Name = d.Name, Description = d.Description, CreatedAt = d.CreatedAt,
            EmployeeCount = d.Employees.Count
        }).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DepartmentDto>> Get(int id)
    {
        var dto = await _db.Departments.Where(d => d.Id == id).Select(d => new DepartmentDto
        {
            Id = d.Id, Name = d.Name, Description = d.Description, CreatedAt = d.CreatedAt,
            EmployeeCount = d.Employees.Count
        }).FirstOrDefaultAsync();
        return dto == null ? NotFound(new { message = "Department not found." }) : Ok(dto);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(DepartmentSaveDto dto)
    {
        var name = dto.Name.Trim();
        if (await _db.Departments.AnyAsync(d => d.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "Department name already exists." });
        var d = new Department { Name = name, Description = dto.Description?.Trim(), CreatedAt = DateTime.UtcNow };
        _db.Departments.Add(d);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = d.Id },
            new DepartmentDto { Id = d.Id, Name = d.Name, Description = d.Description, CreatedAt = d.CreatedAt });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DepartmentDto>> Update(int id, DepartmentSaveDto dto)
    {
        var d = await _db.Departments.FindAsync(id);
        if (d == null) return NotFound(new { message = "Department not found." });
        var name = dto.Name.Trim();
        if (await _db.Departments.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "Department name already exists." });
        d.Name = name;
        d.Description = dto.Description?.Trim();
        await _db.SaveChangesAsync();
        return await Get(id);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.Departments.FindAsync(id);
        if (d == null) return NotFound(new { message = "Department not found." });
        if (await _db.Employees.AnyAsync(e => e.DepartmentId == id))
            return Conflict(new { message = "Cannot delete a department that has employees." });
        _db.Departments.Remove(d);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
