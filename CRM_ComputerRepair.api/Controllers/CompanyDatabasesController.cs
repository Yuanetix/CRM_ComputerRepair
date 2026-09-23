using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("company-databases")]
[Authorize(Roles = "Admin,Super Admin")]
public class CompanyDatabasesController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public CompanyDatabasesController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rows = await _db.CompanyDatabases.AsNoTracking().ToListAsync();
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompanyDatabase cd)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        _db.CompanyDatabases.Add(cd);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { }, cd);
    }
}