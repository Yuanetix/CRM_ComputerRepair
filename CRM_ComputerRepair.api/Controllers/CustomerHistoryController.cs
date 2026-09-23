using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/customer-history")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class CustomerHistoryController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public CustomerHistoryController(ITenantDbContextFactory factory) => _factory = factory;

    /// <summary>
    /// Complete history for one customer: profile + repairs + interactions + follow-ups.
    /// </summary>
    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> GetHistory(int companyId, int customerId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return NotFound();

        var repairs = await db.RepairRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.RequestDate)
            .Select(r => new CustomerHistoryRepairDto
            {
                RepairRequestId = r.RepairRequestId,
                RequestNumber = r.RequestNumber,
                DeviceModel = r.DeviceModel,
                IssueDescription = r.IssueDescription,
                Status = (int)r.Status,
                Priority = (int)r.Priority,
                RequestDate = r.RequestDate,
                CompletionDate = r.CompletionDate,
                ActualCost = r.ActualCost
            })
            .ToListAsync();

        var interactions = await db.CustomerInteractions.AsNoTracking()
            .Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InteractionDate)
            .Select(i => new CustomerHistoryInteractionDto
            {
                CustomerInteractionId = i.CustomerInteractionId,
                InteractionType = (int)i.InteractionType,
                Status = (int)i.Status,
                Priority = (int)i.Priority,
                Subject = i.Subject,
                Notes = i.Notes,
                Resolution = i.Resolution,
                InteractionDate = i.InteractionDate,
                ClosedAt = i.ClosedAt
            })
            .ToListAsync();

        var followUps = await db.FollowUps.AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.ScheduledAt)
            .Select(f => new CustomerHistoryFollowUpDto
            {
                FollowUpId = f.FollowUpId,
                Subject = f.Subject,
                Notes = f.Notes,
                Channel = (int)f.Channel,
                Status = (int)f.Status,
                ScheduledAt = f.ScheduledAt,
                CompletedAt = f.CompletedAt
            })
            .ToListAsync();

        return Ok(new CustomerHistoryDto
        {
            CustomerId = customer.CustomerId,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email,
            Phone = customer.Phone,
            Address = customer.Address,
            LoyaltyPoints = customer.LoyaltyPoints,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            Repairs = repairs,
            Interactions = interactions,
            FollowUps = followUps
        });
    }
}