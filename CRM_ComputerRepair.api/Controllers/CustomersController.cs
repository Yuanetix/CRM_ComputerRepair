using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/customers")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class CustomersController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly IAuditWriter _audit;

    public CustomersController(ITenantDbContextFactory factory, IAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    // --- GET ALL ---
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId, [FromQuery] bool? includeArchived)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.Customers.AsNoTracking();
        if (includeArchived != true)
            query = query.Where(c => c.IsActive);

        var list = await query.OrderBy(x => x.CustomerId).ToListAsync();
        return Ok(list);
    }

    // --- GET ONE ---
    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetById(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        return customer is null ? NotFound() : Ok(customer);
    }

    // --- CREATE ---
    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreateCustomerRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = new Customer
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email?.Trim() ?? "",
            Phone = request.Phone?.Trim() ?? "",
            Address = request.Address?.Trim() ?? "",
            LoyaltyPoints = request.LoyaltyPoints ?? 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Create", "Customer",
            customer.CustomerId.ToString(),
            $"{customer.FirstName} {customer.LastName}");

        return CreatedAtAction(nameof(GetById),
            new { companyId, customerId = customer.CustomerId }, customer);
    }

    // --- UPDATE ---
    [HttpPut("{customerId:int}")]
    public async Task<IActionResult> Update(
        int companyId, int customerId, [FromBody] UpdateCustomerRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound();

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = request.Email?.Trim() ?? "";
        customer.Phone = request.Phone?.Trim() ?? "";
        customer.Address = request.Address?.Trim() ?? "";

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Update", "Customer",
            customerId.ToString(),
            $"{customer.FirstName} {customer.LastName}");

        return Ok(customer);
    }

    // --- ARCHIVE (soft delete) ---
    [HttpDelete("{customerId:int}")]
    public async Task<IActionResult> Archive(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound();

        customer.IsActive = false;
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Archive", "Customer",
            customerId.ToString(),
            $"{customer.FirstName} {customer.LastName}");

        return Ok(new
        {
            message = $"Customer {customerId} archived.",
            customer.CustomerId,
            customer.IsActive
        });
    }

    // --- RESTORE ---
    [HttpPost("{customerId:int}/restore")]
    public async Task<IActionResult> Restore(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound();

        customer.IsActive = true;
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Restore", "Customer",
            customerId.ToString(),
            $"{customer.FirstName} {customer.LastName}");

        return Ok(new
        {
            message = $"Customer {customerId} restored.",
            customer.CustomerId,
            customer.IsActive
        });
    }
}