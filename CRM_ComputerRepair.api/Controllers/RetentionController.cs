using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/retention")]
public class RetentionController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public RetentionController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpPost("{customerId:int}/contact")]
    public async Task<IActionResult> LogContact(
        int companyId, int customerId,
        [FromBody] RetentionContactRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null)
            return NotFound($"Customer {customerId} not found.");

        var interaction = new CustomerInteraction
        {
            CustomerId = customerId,
            InteractionType = InteractionType.Inquiry,
            Status = InteractionStatus.Open,
            Priority = InteractionPriority.Medium,
            Subject = request.Subject.Trim(),
            Notes = request.Notes.Trim(),
            InteractionByUserId = request.PerformedByUserId ?? "staff-001",
            InteractionDate = DateTime.UtcNow,
            IsActive = true
        };

        db.CustomerInteractions.Add(interaction);
        await db.SaveChangesAsync();

        if (request.ScheduleFollowUpInDays.HasValue &&
            request.ScheduleFollowUpInDays.Value > 0)
        {
            var followUp = new FollowUp
            {
                CustomerId = customerId,
                Subject = $"Follow up: {request.Subject.Trim()}",
                Notes = "Auto-created from Retention outreach.",
                ScheduledAt = DateTime.UtcNow.AddDays(request.ScheduleFollowUpInDays.Value),
                Channel = FollowUpChannel.Call,
                Status = FollowUpStatus.Scheduled,
                AssignedToUserId = request.PerformedByUserId ?? "staff-001",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.FollowUps.Add(followUp);
            await db.SaveChangesAsync();
        }

        return Ok(new
        {
            message = $"Retention contact logged for customer {customerId}.",
            interactionId = interaction.CustomerInteractionId,
            customerId
        });
    }
}