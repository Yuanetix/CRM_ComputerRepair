using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/customers")]
public class CustomersController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public CustomersController(ITenantDbContextFactory factory) => _factory = factory;

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

    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetById(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        return customer is null ? NotFound() : Ok(customer);
    }

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

        return CreatedAtAction(nameof(GetById),
            new { companyId, customerId = customer.CustomerId }, customer);
    }

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
        return Ok(customer);
    }

    [HttpDelete("{customerId:int}")]
    public async Task<IActionResult> Archive(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound();

        customer.IsActive = false;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Customer {customerId} archived.",
            customer.CustomerId,
            customer.IsActive
        });
    }

    [HttpPost("{customerId:int}/restore")]
    public async Task<IActionResult> Restore(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound();

        customer.IsActive = true;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Customer {customerId} restored.",
            customer.CustomerId,
            customer.IsActive
        });
    }
}