using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/interactions")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class InteractionsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public InteractionsController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] InteractionType? type,
        [FromQuery] bool? includeArchived,
        [FromQuery] string? search = null)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.CustomerInteractions.AsNoTracking();
        if (includeArchived != true)
            query = query.Where(x => x.IsActive);
        if (type.HasValue)
            query = query.Where(x => x.InteractionType == type.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.Subject.Contains(s) ||
                x.Notes.Contains(s) ||
                (x.Resolution != null && x.Resolution.Contains(s)) ||
                (x.Customer != null && (x.Customer.FirstName.Contains(s) || x.Customer.LastName.Contains(s) || (x.Customer.Phone != null && x.Customer.Phone.Contains(s)))) ||
                (x.RepairRequest != null && (x.RepairRequest.RequestNumber.Contains(s) || x.RepairRequest.DeviceModel.Contains(s))));
        }

        var list = await query
            .OrderByDescending(x => x.InteractionDate)
            .Select(x => new InteractionResponseDto
            {
                CustomerInteractionId = x.CustomerInteractionId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? (x.Customer.FirstName + " " + x.Customer.LastName).Trim() : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerEmail = x.Customer != null ? x.Customer.Email : null,
                RepairRequestId = x.RepairRequestId,
                RepairRequestNumber = x.RepairRequest != null ? x.RepairRequest.RequestNumber : null,
                DeviceModel = x.RepairRequest != null ? x.RepairRequest.DeviceModel : null,
                InteractionType = (int)x.InteractionType,
                Status = (int)x.Status,
                Priority = (int)x.Priority,
                Subject = x.Subject,
                Notes = x.Notes,
                Resolution = x.Resolution,
                InteractionByUserId = x.InteractionByUserId,
                InteractionDate = x.InteractionDate,
                UpdatedAt = x.UpdatedAt,
                ClosedAt = x.ClosedAt,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{interactionId:int}")]
    public async Task<IActionResult> GetById(int companyId, int interactionId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.CustomerInteractions.AsNoTracking()
            .Where(x => x.CustomerInteractionId == interactionId)
            .Select(x => new InteractionResponseDto
            {
                CustomerInteractionId = x.CustomerInteractionId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? (x.Customer.FirstName + " " + x.Customer.LastName).Trim() : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerEmail = x.Customer != null ? x.Customer.Email : null,
                RepairRequestId = x.RepairRequestId,
                RepairRequestNumber = x.RepairRequest != null ? x.RepairRequest.RequestNumber : null,
                DeviceModel = x.RepairRequest != null ? x.RepairRequest.DeviceModel : null,
                InteractionType = (int)x.InteractionType,
                Status = (int)x.Status,
                Priority = (int)x.Priority,
                Subject = x.Subject,
                Notes = x.Notes,
                Resolution = x.Resolution,
                InteractionByUserId = x.InteractionByUserId,
                InteractionDate = x.InteractionDate,
                UpdatedAt = x.UpdatedAt,
                ClosedAt = x.ClosedAt,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync();

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

        var responseDto = await LoadResponseDtoAsync(db, interaction.CustomerInteractionId);
        return CreatedAtAction(nameof(GetById),
            new { companyId, interactionId = interaction.CustomerInteractionId },
            responseDto);
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
        item.InteractionByUserId = request.InteractionByUserId ?? item.InteractionByUserId;
        item.UpdatedAt = DateTime.UtcNow;

        if (item.Status == InteractionStatus.Closed && item.ClosedAt is null)
            item.ClosedAt = DateTime.UtcNow;
        if (item.Status != InteractionStatus.Closed)
            item.ClosedAt = null;

        await db.SaveChangesAsync();

        var responseDto = await LoadResponseDtoAsync(db, item.CustomerInteractionId);
        return Ok(responseDto);
    }

    [HttpPost("{interactionId:int}/resolve")]
    public async Task<IActionResult> Resolve(
        int companyId, int interactionId,
        [FromBody] ResolveInteractionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.CustomerInteractions
            .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

        if (item is null) return NotFound();

        item.Status = InteractionStatus.Closed;
        item.ClosedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        item.Resolution = request.Resolution.Trim();

        await db.SaveChangesAsync();

        var responseDto = await LoadResponseDtoAsync(db, item.CustomerInteractionId);
        return Ok(responseDto);
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

    private static async Task<InteractionResponseDto?> LoadResponseDtoAsync(
        CRM_ComputerRepair.infrastructure.Data.TenantCrmDbContext db, int interactionId)
    {
        return await db.CustomerInteractions.AsNoTracking()
            .Where(x => x.CustomerInteractionId == interactionId)
            .Select(x => new InteractionResponseDto
            {
                CustomerInteractionId = x.CustomerInteractionId,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer != null ? (x.Customer.FirstName + " " + x.Customer.LastName).Trim() : null,
                CustomerPhone = x.Customer != null ? x.Customer.Phone : null,
                CustomerEmail = x.Customer != null ? x.Customer.Email : null,
                RepairRequestId = x.RepairRequestId,
                RepairRequestNumber = x.RepairRequest != null ? x.RepairRequest.RequestNumber : null,
                DeviceModel = x.RepairRequest != null ? x.RepairRequest.DeviceModel : null,
                InteractionType = (int)x.InteractionType,
                Status = (int)x.Status,
                Priority = (int)x.Priority,
                Subject = x.Subject,
                Notes = x.Notes,
                Resolution = x.Resolution,
                InteractionByUserId = x.InteractionByUserId,
                InteractionDate = x.InteractionDate,
                UpdatedAt = x.UpdatedAt,
                ClosedAt = x.ClosedAt,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync();
    }
}