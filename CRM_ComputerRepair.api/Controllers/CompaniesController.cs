using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("companies")]
[Authorize(Roles = "Admin,Super Admin")]
public class CompaniesController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public CompaniesController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var companies = await _db.Companies
            .Include(c => c.Devices)
            .AsNoTracking()
            .ToListAsync();
        return Ok(companies);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var company = await _db.Companies
            .Include(c => c.Devices)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == id);

        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Company company)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { id = company.CompanyId }, company);
    }
}