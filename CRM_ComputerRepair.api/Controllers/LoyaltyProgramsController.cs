using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("loyalty-programs")]
public class LoyaltyProgramsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public LoyaltyProgramsController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
    {
        var query = _db.LoyaltyPrograms.AsNoTracking();
        if (activeOnly == true)
            query = query.Where(x => x.IsActive);

        var list = await query
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.LoyaltyPrograms.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LoyaltyProgramId == id);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoyaltyProgramRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var program = new LoyaltyProgram
        {
            ProgramName = request.ProgramName.Trim(),
            Description = request.Description?.Trim() ?? "",
            PointsPerPeso = request.PointsPerPeso,
            DiscountPercentage = request.DiscountPercentage,
            MinimumSpend = request.MinimumSpend,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.LoyaltyPrograms.Add(program);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { id = program.LoyaltyProgramId }, program);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,
        [FromBody] UpdateLoyaltyProgramRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var item = await _db.LoyaltyPrograms
            .FirstOrDefaultAsync(x => x.LoyaltyProgramId == id);

        if (item is null) return NotFound();

        item.ProgramName = request.ProgramName.Trim();
        item.Description = request.Description?.Trim() ?? "";
        item.PointsPerPeso = request.PointsPerPeso;
        item.DiscountPercentage = request.DiscountPercentage;
        item.MinimumSpend = request.MinimumSpend;
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _db.LoyaltyPrograms
            .FirstOrDefaultAsync(x => x.LoyaltyProgramId == id);

        if (item is null) return NotFound();

        item.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Loyalty program {id} archived." });
    }
}