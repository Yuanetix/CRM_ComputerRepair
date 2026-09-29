using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("subscriptions")]
[Authorize(Roles = "Super Admin,Admin")]
public class SubscriptionsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;
    private readonly IAuditWriter _audit;

    public SubscriptionsController(MasterCrmDbContext db, IAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null, [FromQuery] bool includeArchived = false)
    {
        var query = _db.Subscriptions.AsNoTracking();

        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsArchived);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.SubscriptionId)
            .ToListAsync();

        var companySubCounts = await _db.Companies
            .AsNoTracking()
            .Where(c => c.SubscriptionId.HasValue && c.IsActive)
            .GroupBy(c => c.SubscriptionId!.Value)
            .Select(g => new { SubscriptionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubscriptionId, x => x.Count);

        var dtos = list.Select(s => new SubscriptionPlanDetailDto
        {
            SubscriptionId = s.SubscriptionId,
            CompanyId = s.CompanyId,
            SubscriptionName = s.SubscriptionName,
            PricePerMonth = s.PricePerMonth,
            DurationMonths = s.DurationMonths,
            Duration = s.Duration ?? $"{s.DurationMonths} Month(s)",
            MaxUsers = s.MaxUsers,
            MaxDevices = s.MaxDevices,
            EnableMultiBranching = s.EnableMultiBranching,
            Description = s.Description,
            IsActive = s.IsActive,
            IsArchived = s.IsArchived,
            BillingCycle = s.BillingCycle,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            SubscribedCompaniesCount = companySubCounts.TryGetValue(s.SubscriptionId, out int count) ? count : 0
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await _db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriptionId == id);

        if (s is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        int activeSubs = await _db.Companies
            .AsNoTracking()
            .CountAsync(c => c.SubscriptionId == id && c.IsActive);

        var dto = new SubscriptionPlanDetailDto
        {
            SubscriptionId = s.SubscriptionId,
            CompanyId = s.CompanyId,
            SubscriptionName = s.SubscriptionName,
            PricePerMonth = s.PricePerMonth,
            DurationMonths = s.DurationMonths,
            Duration = s.Duration ?? $"{s.DurationMonths} Month(s)",
            MaxUsers = s.MaxUsers,
            MaxDevices = s.MaxDevices,
            EnableMultiBranching = s.EnableMultiBranching,
            Description = s.Description,
            IsActive = s.IsActive,
            IsArchived = s.IsArchived,
            BillingCycle = s.BillingCycle,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            SubscribedCompaniesCount = activeSubs
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var durationStr = string.IsNullOrWhiteSpace(request.Duration)
            ? $"{request.DurationMonths} Month(s)"
            : request.Duration.Trim();

        var sub = new Subscription
        {
            SubscriptionName = request.SubscriptionName.Trim(),
            PricePerMonth = request.PricePerMonth,
            DurationMonths = request.DurationMonths > 0 ? request.DurationMonths : 1,
            Duration = durationStr,
            MaxUsers = request.MaxUsers > 0 ? request.MaxUsers : 5,
            MaxDevices = request.MaxDevices > 0 ? request.MaxDevices : 100,
            EnableMultiBranching = request.EnableMultiBranching,
            Description = request.Description?.Trim(),
            BillingCycle = request.BillingCycle ?? "Monthly",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(request.DurationMonths > 0 ? request.DurationMonths : 1),
            IsActive = true,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("Create", "Subscription", sub.SubscriptionId.ToString(),
            $"Created subscription plan '{sub.SubscriptionName}' (₱{sub.PricePerMonth:N2}, {sub.Duration}, Multi-branching: {sub.EnableMultiBranching}).");

        return CreatedAtAction(nameof(GetById), new { id = sub.SubscriptionId }, new SubscriptionPlanDetailDto
        {
            SubscriptionId = sub.SubscriptionId,
            SubscriptionName = sub.SubscriptionName,
            PricePerMonth = sub.PricePerMonth,
            DurationMonths = sub.DurationMonths,
            Duration = sub.Duration,
            MaxUsers = sub.MaxUsers,
            MaxDevices = sub.MaxDevices,
            EnableMultiBranching = sub.EnableMultiBranching,
            Description = sub.Description,
            IsActive = sub.IsActive,
            IsArchived = sub.IsArchived,
            BillingCycle = sub.BillingCycle,
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            SubscribedCompaniesCount = 0
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSubscriptionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var item = await _db.Subscriptions
            .FirstOrDefaultAsync(x => x.SubscriptionId == id);

        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.SubscriptionName = request.SubscriptionName.Trim();
        item.PricePerMonth = request.PricePerMonth;
        item.DurationMonths = request.DurationMonths > 0 ? request.DurationMonths : 1;
        item.Duration = string.IsNullOrWhiteSpace(request.Duration)
            ? $"{item.DurationMonths} Month(s)"
            : request.Duration.Trim();
        item.MaxUsers = request.MaxUsers > 0 ? request.MaxUsers : 1;
        item.MaxDevices = request.MaxDevices > 0 ? request.MaxDevices : 1;
        item.EnableMultiBranching = request.EnableMultiBranching;
        item.Description = request.Description?.Trim();
        item.IsActive = request.IsActive;
        item.IsArchived = request.IsArchived;
        item.BillingCycle = request.BillingCycle ?? item.BillingCycle;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("Update", "Subscription", item.SubscriptionId.ToString(),
            $"Updated subscription plan '{item.SubscriptionName}'.");

        return await GetById(id);
    }

    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var item = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id);
        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.IsActive = !item.IsActive;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        string action = item.IsActive ? "Activated" : "Deactivated";
        await _audit.WriteAsync(action, "Subscription", item.SubscriptionId.ToString(),
            $"{action} subscription plan '{item.SubscriptionName}'.");

        return Ok(new
        {
            message = $"Subscription plan '{item.SubscriptionName}' is now {action.ToLowerInvariant()}.",
            isActive = item.IsActive
        });
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id);
        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.IsArchived = true;
        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("Archive", "Subscription", item.SubscriptionId.ToString(),
            $"Archived subscription plan '{item.SubscriptionName}'.");

        return Ok(new { message = $"Subscription plan '{item.SubscriptionName}' has been archived." });
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var item = await _db.Subscriptions.FirstOrDefaultAsync(x => x.SubscriptionId == id);
        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.IsArchived = false;
        item.IsActive = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("Restore", "Subscription", item.SubscriptionId.ToString(),
            $"Restored subscription plan '{item.SubscriptionName}'.");

        return Ok(new { message = $"Subscription plan '{item.SubscriptionName}' has been restored and activated." });
    }
}