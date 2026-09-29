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
        [FromQuery] bool? includeArchived,
        [FromQuery] string? search = null)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.FollowUps.AsNoTracking();
        if (includeArchived != true)
            query = query.Where(x => x.IsActive);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.Subject.Contains(s) ||
                x.Notes.Contains(s) ||
                (x.AssignedToUserId != null && x.AssignedToUserId.Contains(s)) ||
                (x.Customer != null && (x.Customer.FirstName.Contains(s) || x.Customer.LastName.Contains(s) || (x.Customer.Phone != null && x.Customer.Phone.Contains(s)))) ||
                (x.RepairRequest != null && (x.RepairRequest.RequestNumber.Contains(s) || x.RepairRequest.DeviceModel.Contains(s))));
        }

        var list = await query
            .OrderBy(x => x.ScheduledAt)
            .Select(x => new FollowUpResponseDto
            {
                FollowUpId = x.FollowUpId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? (x.Customer.FirstName + " " + x.Customer.LastName).Trim() : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerEmail = x.Customer != null ? x.Customer.Email : null,
                RepairRequestId = x.RepairRequestId,
                RepairRequestNumber = x.RepairRequest != null ? x.RepairRequest.RequestNumber : null,
                DeviceModel = x.RepairRequest != null ? x.RepairRequest.DeviceModel : null,
                Subject = x.Subject,
                Notes = x.Notes,
                ScheduledAt = x.ScheduledAt,
                CompletedAt = x.CompletedAt,
                Channel = (int)x.Channel,
                Status = (int)x.Status,
                AssignedToUserId = x.AssignedToUserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{followUpId:int}")]
    public async Task<IActionResult> GetById(int companyId, int followUpId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.FollowUps.AsNoTracking()
            .Where(x => x.FollowUpId == followUpId)
            .Select(x => new FollowUpResponseDto
            {
                FollowUpId = x.FollowUpId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? (x.Customer.FirstName + " " + x.Customer.LastName).Trim() : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerEmail = x.Customer != null ? x.Customer.Email : null,
                RepairRequestId = x.RepairRequestId,
                RepairRequestNumber = x.RepairRequest != null ? x.RepairRequest.RequestNumber : null,
                DeviceModel = x.RepairRequest != null ? x.RepairRequest.DeviceModel : null,
                Subject = x.Subject,
                Notes = x.Notes,
                ScheduledAt = x.ScheduledAt,
                CompletedAt = x.CompletedAt,
                Channel = (int)x.Channel,
                Status = (int)x.Status,
                AssignedToUserId = x.AssignedToUserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync();

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

        var responseDto = await LoadResponseDtoAsync(db, followUp.FollowUpId);
        return CreatedAtAction(nameof(GetById),
            new { companyId, followUpId = followUp.FollowUpId }, responseDto);
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

        var responseDto = await LoadResponseDtoAsync(db, item.FollowUpId);
        return Ok(responseDto);
    }

    [HttpPost("{followUpId:int}/complete")]
    public async Task<IActionResult> Complete(
        int companyId, int followUpId, [FromBody] CompleteFollowUpRequest request)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.FollowUps.FirstOrDefaultAsync(x => x.FollowUpId == followUpId);
        if (item is null) return NotFound();

        item.Status = FollowUpStatus.Completed;
        item.CompletedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.OutcomeNotes))
        {
            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            item.Notes = string.IsNullOrWhiteSpace(item.Notes)
                ? $"[Outcome {stamp}]: {request.OutcomeNotes.Trim()}"
                : $"{item.Notes}\n\n[Outcome {stamp}]: {request.OutcomeNotes.Trim()}";
        }

        if (request.LogInteraction && item.CustomerId.HasValue)
        {
            var interaction = new CustomerInteraction
            {
                CustomerId = item.CustomerId.Value,
                RepairRequestId = item.RepairRequestId,
                Subject = $"Follow-up: {item.Subject}",
                Notes = string.IsNullOrWhiteSpace(request.OutcomeNotes) ? item.Notes : request.OutcomeNotes.Trim(),
                InteractionType = InteractionType.Inquiry,
                InteractionDate = DateTime.UtcNow,
                Status = InteractionStatus.Closed,
                ClosedAt = DateTime.UtcNow,
                Resolution = "Follow-up completed successfully.",
                InteractionByUserId = item.AssignedToUserId,
                IsActive = true
            };
            db.CustomerInteractions.Add(interaction);
        }

        await db.SaveChangesAsync();

        var responseDto = await LoadResponseDtoAsync(db, item.FollowUpId);
        return Ok(responseDto);
    }

    [HttpPost("{followUpId:int}/reschedule")]
    public async Task<IActionResult> Reschedule(
        int companyId, int followUpId, [FromBody] RescheduleFollowUpRequest request)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.FollowUps.FirstOrDefaultAsync(x => x.FollowUpId == followUpId);
        if (item is null) return NotFound();

        item.ScheduledAt = request.NewScheduledAt;
        item.Status = FollowUpStatus.Scheduled;
        item.CompletedAt = null;
        item.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            var noteLine = $"[Rescheduled to {request.NewScheduledAt:yyyy-MM-dd HH:mm} on {stamp}]: {request.Reason.Trim()}";
            item.Notes = string.IsNullOrWhiteSpace(item.Notes)
                ? noteLine
                : $"{item.Notes}\n\n{noteLine}";
        }

        await db.SaveChangesAsync();

        var responseDto = await LoadResponseDtoAsync(db, item.FollowUpId);
        return Ok(responseDto);
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

    private static async Task<FollowUpResponseDto?> LoadResponseDtoAsync(CRM_ComputerRepair.infrastructure.Data.TenantCrmDbContext db, int followUpId)
    {
        return await db.FollowUps.AsNoTracking()
            .Where(x => x.FollowUpId == followUpId)
            .Select(x => new FollowUpResponseDto
            {
                FollowUpId = x.FollowUpId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? (x.Customer.FirstName + " " + x.Customer.LastName).Trim() : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerEmail = x.Customer != null ? x.Customer.Email : null,
                RepairRequestId = x.RepairRequestId,
                RepairRequestNumber = x.RepairRequest != null ? x.RepairRequest.RequestNumber : null,
                DeviceModel = x.RepairRequest != null ? x.RepairRequest.DeviceModel : null,
                Subject = x.Subject,
                Notes = x.Notes,
                ScheduledAt = x.ScheduledAt,
                CompletedAt = x.CompletedAt,
                Channel = (int)x.Channel,
                Status = (int)x.Status,
                AssignedToUserId = x.AssignedToUserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync();
    }
}