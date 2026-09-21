using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public AnalyticsController(ITenantDbContextFactory factory) => _factory = factory;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ninetyDaysAgo = now.AddDays(-90);

        var allCustomers = await db.Customers.AsNoTracking().ToListAsync();
        var totalCustomers = allCustomers.Count;
        var activeCustomers = allCustomers.Count(c => c.IsActive);
        var newThisMonth = allCustomers.Count(c => c.CreatedAt >= startOfMonth);

        var allRepairs = await db.RepairRequests.AsNoTracking().ToListAsync();
        var completedThisMonth = allRepairs.Count(r =>
            r.Status == RepairStatus.Completed &&
            r.CompletionDate.HasValue &&
            r.CompletionDate.Value >= startOfMonth);

        var completedRepairs = allRepairs
            .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
            .ToList();

        var avgTurnaround = completedRepairs.Any()
            ? Math.Round(completedRepairs.Average(r =>
                (r.CompletionDate!.Value - r.RequestDate).TotalDays), 1)
            : 0.0;

        var customersWithRepairs = allRepairs
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count());

        var repeatCustomerRate = customersWithRepairs.Any()
            ? Math.Round(100.0 * customersWithRepairs.Count(kv => kv.Value >= 2)
                / customersWithRepairs.Count, 1)
            : 0.0;

        var retentionRate = totalCustomers > 0
            ? Math.Round(100.0 * customersWithRepairs.Count(kv => kv.Value >= 2)
                / totalCustomers, 1)
            : 0.0;

        var allInteractions = await db.CustomerInteractions.AsNoTracking().ToListAsync();
        var openInteractions = allInteractions.Count(i => i.Status != InteractionStatus.Closed);

        var recentlyActiveIds = allInteractions
            .Where(i => i.InteractionDate >= ninetyDaysAgo && i.CustomerId.HasValue)
            .Select(i => i.CustomerId!.Value).Distinct().ToHashSet();

        var churnRisk = allCustomers.Count(c =>
            c.IsActive && !recentlyActiveIds.Contains(c.CustomerId));

        var customersOverTime = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            customersOverTime.Add(new
            {
                month = monthStart.ToString("yyyy-MM"),
                label = monthStart.ToString("MMM yyyy"),
                count = allCustomers.Count(c => c.CreatedAt >= monthStart && c.CreatedAt < monthEnd)
            });
        }

        var retentionTrend = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            retentionTrend.Add(new
            {
                month = monthStart.ToString("yyyy-MM"),
                label = monthStart.ToString("MMM yyyy"),
                active = allInteractions.Where(x =>
                    x.InteractionDate >= monthStart && x.InteractionDate < monthEnd &&
                    x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).Distinct().Count()
            });
        }

        return Ok(new
        {
            totalCustomers,
            activeCustomers,
            newThisMonth,
            retentionRate,
            churnRisk,
            openInteractions,
            repairsCompletedThisMonth = completedThisMonth,
            averageTurnaroundDays = avgTurnaround,
            repeatCustomerRate,
            interactionsByType = new
            {
                inquiry = allInteractions.Count(i => i.InteractionType == InteractionType.Inquiry),
                complaint = allInteractions.Count(i => i.InteractionType == InteractionType.Complaint),
                feedback = allInteractions.Count(i => i.InteractionType == InteractionType.Feedback)
            },
            repairsByStatus = new
            {
                pending = allRepairs.Count(r => r.Status == RepairStatus.Pending),
                approved = allRepairs.Count(r => r.Status == RepairStatus.Approved),
                inProgress = allRepairs.Count(r => r.Status == RepairStatus.InProgress),
                completed = allRepairs.Count(r => r.Status == RepairStatus.Completed),
                rejected = allRepairs.Count(r => r.Status == RepairStatus.Rejected),
                reassigned = allRepairs.Count(r => r.Status == RepairStatus.Reassigned)
            },
            customersOverTime,
            retentionTrend
        });
    }

    [HttpGet("retention-candidates")]
    public async Task<IActionResult> RetentionCandidates(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);

        var recentIds = await db.CustomerInteractions
            .Where(i => i.InteractionDate >= ninetyDaysAgo && i.CustomerId.HasValue)
            .Select(i => i.CustomerId!.Value).Distinct().ToListAsync();

        var candidates = await db.Customers.AsNoTracking()
            .Where(c => c.IsActive && !recentIds.Contains(c.CustomerId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return Ok(candidates);
    }
}