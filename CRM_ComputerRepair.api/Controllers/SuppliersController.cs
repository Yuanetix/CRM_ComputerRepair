using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using CRM_ComputerRepair.api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/suppliers")]
[Authorize(Roles = "Manager,Admin,Super Admin")]
[RequireSubscribedModule(ModuleCodes.MainTransactions)]
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
            ContactFirstName = !string.IsNullOrWhiteSpace(request.ContactFirstName)
                ? request.ContactFirstName.Trim()
                : (request.ContactPerson?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()),
            ContactLastName = !string.IsNullOrWhiteSpace(request.ContactLastName)
                ? request.ContactLastName.Trim()
                : (request.ContactPerson?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()),
            ContactNumber = request.ContactNumber?.Trim(),
            EmailAddress = request.EmailAddress?.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            StateOrProvince = request.StateOrProvince?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "Philippines" : request.Country.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Suppliers.Add(s);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { companyId }, s);
    }
}