using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/follow-ups")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class FollowUpsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public FollowUpsController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] FollowUpStatus? status,
        [FromQuery] bool? includeArchived)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.FollowUps.AsNoTracking();
        if (includeArchived != true)
            query = query.Where(x => x.IsActive);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var list = await query.OrderBy(x => x.ScheduledAt).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{followUpId:int}")]
    public async Task<IActionResult> GetById(int companyId, int followUpId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.FollowUps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreateFollowUpRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var followUp = new FollowUp
        {
            CustomerId = request.CustomerId,
            RepairRequestId = request.RepairRequestId,
            Subject = request.Subject.Trim(),
            Notes = request.Notes.Trim(),
            ScheduledAt = request.ScheduledAt,
            Channel = (FollowUpChannel)request.Channel,
            Status = (FollowUpStatus)request.Status,
            AssignedToUserId = request.AssignedToUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        if (followUp.Status == FollowUpStatus.Completed)
            followUp.CompletedAt = DateTime.UtcNow;

        db.FollowUps.Add(followUp);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById),
            new { companyId, followUpId = followUp.FollowUpId }, followUp);
    }

    [HttpPut("{followUpId:int}")]
    public async Task<IActionResult> Update(
        int companyId, int followUpId, [FromBody] UpdateFollowUpRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.FollowUps
            .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

        if (item is null) return NotFound();

        item.CustomerId = request.CustomerId;
        item.RepairRequestId = request.RepairRequestId;
        item.Subject = request.Subject.Trim();
        item.Notes = request.Notes.Trim();
        item.ScheduledAt = request.ScheduledAt;
        item.Channel = (FollowUpChannel)request.Channel;
        item.Status = (FollowUpStatus)request.Status;
        item.AssignedToUserId = request.AssignedToUserId;
        item.UpdatedAt = DateTime.UtcNow;

        if (item.Status == FollowUpStatus.Completed && item.CompletedAt is null)
            item.CompletedAt = DateTime.UtcNow;
        if (item.Status != FollowUpStatus.Completed)
            item.CompletedAt = null;

        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{followUpId:int}")]
    public async Task<IActionResult> Archive(int companyId, int followUpId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.FollowUps
            .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

        if (item is null) return NotFound();

        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = $"Follow-up {followUpId} archived." });
    }

    [HttpPost("{followUpId:int}/restore")]
    public async Task<IActionResult> Restore(int companyId, int followUpId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.FollowUps
            .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

        if (item is null) return NotFound();

        item.IsActive = true;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = $"Follow-up {followUpId} restored." });
    }
}