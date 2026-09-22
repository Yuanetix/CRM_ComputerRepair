using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public SubscriptionsController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
    {
        var query = _db.Subscriptions.AsNoTracking();
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
        var item = await _db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriptionId == id);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var sub = new Subscription
        {
            SubscriptionName = request.SubscriptionName.Trim(),
            PricePerMonth = request.PricePerMonth,
            MaxUsers = request.MaxUsers,
            MaxDevices = request.MaxDevices,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            BillingCycle = request.BillingCycle ?? "Monthly"
        };

        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { id = sub.SubscriptionId }, sub);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,
        [FromBody] UpdateSubscriptionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var item = await _db.Subscriptions
            .FirstOrDefaultAsync(x => x.SubscriptionId == id);

        if (item is null) return NotFound();

        item.SubscriptionName = request.SubscriptionName.Trim();
        item.PricePerMonth = request.PricePerMonth;
        item.MaxUsers = request.MaxUsers;
        item.MaxDevices = request.MaxDevices;
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.IsActive = request.IsActive;
        item.BillingCycle = request.BillingCycle ?? "Monthly";

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _db.Subscriptions
            .FirstOrDefaultAsync(x => x.SubscriptionId == id);

        if (item is null) return NotFound();

        item.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Subscription {id} archived." });
    }
}