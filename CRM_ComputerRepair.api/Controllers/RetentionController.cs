using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/retention")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class RetentionController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly RetentionEngine _engine;

    public RetentionController(ITenantDbContextFactory factory, RetentionEngine engine)
    {
        _factory = factory;
        _engine = engine;
    }

    /// <summary>Per-customer retention metrics computed from the tenant database.</summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(int companyId)
    {
        var metrics = await _engine.ComputeMetricsAsync(companyId);
        return Ok(metrics);
    }

    /// <summary>
    /// Retention recommendations with human-readable basis. Every recommendation
    /// states why (e.g. "8 completed transactions", "₱4,500 total spending").
    /// </summary>
    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(
        int companyId,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] decimal? minSpend)
    {
        var result = await _engine.BuildRecommendationsAsync(companyId,
            new RevenueFilter
            {
                Category = category,
                Search = search,
                MinSpent = minSpend ?? 0
            });

        return Ok(result);
    }

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