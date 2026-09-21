using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/interactions")]
public class InteractionsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public InteractionsController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] InteractionType? type,
        [FromQuery] bool? includeArchived)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.CustomerInteractions.AsNoTracking();
        if (includeArchived != true)
            query = query.Where(x => x.IsActive);
        if (type.HasValue)
            query = query.Where(x => x.InteractionType == type.Value);

        var list = await query
            .OrderByDescending(x => x.InteractionDate)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{interactionId:int}")]
    public async Task<IActionResult> GetById(int companyId, int interactionId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.CustomerInteractions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreateInteractionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var interaction = new CustomerInteraction
        {
            CustomerId = request.CustomerId,
            RepairRequestId = request.RepairRequestId,
            InteractionType = (InteractionType)request.InteractionType,
            Priority = (InteractionPriority)request.Priority,
            Status = InteractionStatus.Open,
            Subject = request.Subject.Trim(),
            Notes = request.Notes.Trim(),
            InteractionByUserId = request.InteractionByUserId,
            InteractionDate = DateTime.UtcNow,
            IsActive = true
        };

        db.CustomerInteractions.Add(interaction);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { companyId, interactionId = interaction.CustomerInteractionId },
            interaction);
    }

    [HttpPut("{interactionId:int}")]
    public async Task<IActionResult> Update(
        int companyId, int interactionId,
        [FromBody] UpdateInteractionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.CustomerInteractions
            .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

        if (item is null) return NotFound();

        item.CustomerId = request.CustomerId;
        item.RepairRequestId = request.RepairRequestId;
        item.InteractionType = (InteractionType)request.InteractionType;
        item.Subject = request.Subject.Trim();
        item.Notes = request.Notes.Trim();
        item.Priority = (InteractionPriority)request.Priority;
        item.Status = (InteractionStatus)request.Status;
        item.Resolution = request.Resolution?.Trim();
        item.UpdatedAt = DateTime.UtcNow;

        if (item.Status == InteractionStatus.Closed && item.ClosedAt is null)
            item.ClosedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{interactionId:int}")]
    public async Task<IActionResult> Archive(int companyId, int interactionId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.CustomerInteractions
            .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

        if (item is null) return NotFound();

        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = $"Interaction {interactionId} archived." });
    }

    [HttpPost("{interactionId:int}/restore")]
    public async Task<IActionResult> Restore(int companyId, int interactionId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.CustomerInteractions
            .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

        if (item is null) return NotFound();

        item.IsActive = true;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = $"Interaction {interactionId} restored." });
    }
}