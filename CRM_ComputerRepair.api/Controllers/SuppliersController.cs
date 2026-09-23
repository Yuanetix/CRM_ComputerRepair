using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/suppliers")]
[Authorize(Roles = "Manager,Admin,Super Admin")]
public class SuppliersController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public SuppliersController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var list = await db.Suppliers.AsNoTracking().ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreateSupplierRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var s = new Supplier
        {
            SupplierCode = request.SupplierCode.Trim(),
            SupplierName = request.SupplierName.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            ContactNumber = request.ContactNumber?.Trim(),
            EmailAddress = request.EmailAddress?.Trim(),
            Address = request.Address?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Suppliers.Add(s);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { companyId }, s);
    }
}