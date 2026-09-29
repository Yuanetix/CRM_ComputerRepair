using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/parts")]
[Authorize(Roles = "Manager,Admin,Super Admin")]
public class PartsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public PartsController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId, [FromQuery] string? search = null)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var query = db.Parts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p =>
                p.PartCode.Contains(s) ||
                p.PartName.Contains(s) ||
                (p.Category != null && p.Category.Contains(s)) ||
                (p.Manufacturer != null && p.Manufacturer.Contains(s)) ||
                (p.Model != null && p.Model.Contains(s)));
        }

        var list = await query.ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreatePartRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var p = new Part
        {
            PartCode = request.PartCode.Trim(),
            PartName = request.PartName.Trim(),
            Category = request.Category?.Trim(),
            Manufacturer = request.Manufacturer?.Trim(),
            Model = request.Model?.Trim(),
            UnitCost = request.UnitCost,
            UnitPrice = request.UnitPrice,
            QuantityOnHand = request.QuantityOnHand,
            ReorderLevel = request.ReorderLevel,
            SupplierId = request.SupplierId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Parts.Add(p);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { companyId }, p);
    }
}