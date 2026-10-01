using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using CRM_ComputerRepair.api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/customers")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
[RequireSubscribedModule(ModuleCodes.DataCollection, ModuleCodes.Actions, ModuleCodes.BusinessIntelligence, ModuleCodes.MainTransactions)]
public class CustomersController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly IAuditWriter _audit;
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _userManager;

    public CustomersController(
        ITenantDbContextFactory factory,
        IAuditWriter audit,
        Microsoft.AspNetCore.Identity.UserManager<User> userManager)
    {
        _factory = factory;
        _audit = audit;
        _userManager = userManager;
    }

    // --- GET ALL ---
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] bool? includeArchived,
        [FromQuery] string? search = null,
        [FromQuery] int? branchId = null)
    {
        var (scopedBranchId, isAllowed) = await BranchScopeHelper.ResolveBranchScopeAsync(HttpContext, _userManager, branchId);
        if (!isAllowed)
            return Forbid();

        await using var db = await _factory.CreateAsync(companyId);

        var query = db.Customers.AsNoTracking();
        if (includeArchived != true)
            query = query.Where(c => c.IsActive);

        if (scopedBranchId.HasValue)
            query = query.Where(c => c.BranchId == scopedBranchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                c.FirstName.Contains(s) ||
                c.LastName.Contains(s) ||
                (c.Email != null && c.Email.Contains(s)) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.Address != null && c.Address.Contains(s)) ||
                (c.City != null && c.City.Contains(s)) ||
                (c.StateOrProvince != null && c.StateOrProvince.Contains(s)) ||
                (c.PostalCode != null && c.PostalCode.Contains(s)));
        }

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

        var (scopedBranchId, _) = await BranchScopeHelper.ResolveBranchScopeAsync(HttpContext, _userManager, request.BranchId);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = new Customer
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email?.Trim() ?? "",
            Phone = request.Phone?.Trim() ?? "",
            Address = request.Address?.Trim() ?? "",
            City = request.City?.Trim() ?? "",
            StateOrProvince = request.StateOrProvince?.Trim() ?? "",
            PostalCode = request.PostalCode?.Trim() ?? "",
            Country = string.IsNullOrWhiteSpace(request.Country) ? "Philippines" : request.Country.Trim(),
            LoyaltyPoints = request.LoyaltyPoints ?? 0,
            BranchId = request.BranchId ?? scopedBranchId,
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
        customer.City = request.City?.Trim() ?? "";
        customer.StateOrProvince = request.StateOrProvince?.Trim() ?? "";
        customer.PostalCode = request.PostalCode?.Trim() ?? "";
        if (request.BranchId.HasValue)
            customer.BranchId = request.BranchId.Value;
        if (!string.IsNullOrWhiteSpace(request.Country))
            customer.Country = request.Country.Trim();

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