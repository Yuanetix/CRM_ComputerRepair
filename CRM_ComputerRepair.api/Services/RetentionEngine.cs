using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Services;

/// <summary>
/// Retention & loyalty engine.
///
/// Analyzes REAL customer history from the tenant database (transaction count,
/// last visit, visit frequency, total spending, previous services, loyalty
/// points) and compares it against configured loyalty-program eligibility rules
/// to produce retention recommendations. Every recommendation carries a human
/// readable <see cref="RetentionRecommendationDto.Basis"/> built from the actual
/// database records — e.g. "8 completed transactions", "₱4,500 total spending",
/// or "45 days since last transaction".
/// </summary>
public class RetentionEngine
{
    private const int ReEngagementInactiveDays = 30;
    private const int ChurnInactiveDays = 90;

    private readonly ITenantDbContextFactory _factory;
    private readonly MasterCrmDbContext _master;

    public RetentionEngine(ITenantDbContextFactory factory, MasterCrmDbContext master)
    {
        _factory = factory;
        _master = master;
    }

    /// <summary>Compute per-customer loyalty/retention metrics from the tenant DB.</summary>
    public async Task<List<CustomerRetentionMetricsDto>> ComputeMetricsAsync(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var now = DateTime.UtcNow;

        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync();

        var completedRepairs = await db.RepairRequests.AsNoTracking()
            .Where(r => r.Status == RepairStatus.Completed &&
                        r.CompletionDate.HasValue)
            .ToListAsync();

        var payments = await db.Payments.AsNoTracking()
            .Where(p => !p.IsVoid && p.IsPaid)
            .ToListAsync();

        var paymentByRepair = payments
            .GroupBy(p => p.RepairRequestId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var interactions = await db.CustomerInteractions.AsNoTracking()
            .Where(i => i.CustomerId.HasValue)
            .ToListAsync();

        var interactionByCustomer = interactions
            .Where(i => i.CustomerId.HasValue)
            .GroupBy(i => i.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(i => i.InteractionDate));

        var repairsByCustomer = completedRepairs
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var metrics = new List<CustomerRetentionMetricsDto>();

        foreach (var customer in customers)
        {
            var repairs = repairsByCustomer.TryGetValue(customer.CustomerId, out var list)
                ? list
                : new List<RepairRequest>();

            var txCount = repairs.Count;
            var totalSpent = repairs.Sum(r =>
                paymentByRepair.TryGetValue(r.RepairRequestId, out var amount) ? amount : 0m);

            DateTime? lastTx = repairs.Count > 0
                ? repairs.Max(r => r.CompletionDate)
                : null;

            var daysSinceLastTx = lastTx.HasValue
                ? (int)(now - lastTx.Value).TotalDays
                : (int)(now - customer.CreatedAt).TotalDays;

            var transactions90 = repairs.Count(r =>
                r.CompletionDate!.Value >= now.AddDays(-90));

            var activeMonths = repairs
                .Select(r => new DateTime(r.CompletionDate!.Value.Year,
                    r.CompletionDate.Value.Month, 1))
                .Distinct().Count();

            var services = repairs
                .Select(r => r.DeviceModel)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            metrics.Add(new CustomerRetentionMetricsDto
            {
                CustomerId = customer.CustomerId,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Phone = customer.Phone,
                LoyaltyPoints = customer.LoyaltyPoints,
                CreatedAt = customer.CreatedAt,
                IsActive = customer.IsActive,
                CompletedTransactions = txCount,
                TotalSpent = totalSpent,
                LastTransactionDate = lastTx,
                DaysSinceLastTransaction = Math.Max(0, daysSinceLastTx),
                ActiveMonths = activeMonths,
                TransactionsLast90Days = transactions90,
                PreviousServices = string.Join(", ", services),
                LastService = services.LastOrDefault()
            });
        }

        return metrics;
    }

    /// <summary>
    /// Build retention recommendations by matching real customer history against
    /// configured loyalty-program eligibility rules plus built-in retention rules.
    /// </summary>
    public async Task<List<RetentionRecommendationDto>> BuildRecommendationsAsync(
        int companyId, RevenueFilter? filter = null)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var now = DateTime.UtcNow;
        var metrics = await ComputeMetricsAsync(companyId);

        // Real completed-repair history per customer — used for visit frequency.
        await using var dbMetrics = await _factory.CreateAsync(companyId);
        var completedByCustomer = (await dbMetrics.RepairRequests.AsNoTracking()
                .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
                .ToListAsync())
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var validPrograms = await _master.LoyaltyPrograms.AsNoTracking()
            .Where(p => p.IsActive &&
                        p.StartDate <= now &&
                        p.EndDate >= now)
            .ToListAsync();

        // Open repairs & open interactions → follow-up triggers.
        var openRepairs = await db.RepairRequests.AsNoTracking()
            .Where(r => r.Status != RepairStatus.Completed &&
                        r.Status != RepairStatus.Rejected)
            .ToListAsync();

        var openInteractions = await db.CustomerInteractions.AsNoTracking()
            .Where(i => i.Status != InteractionStatus.Closed &&
                        i.CustomerId.HasValue)
            .ToListAsync();

        var recommendations = new List<RetentionRecommendationDto>();

        foreach (var m in metrics)
        {
            // ── 1. Loyalty program eligibility (reward / discount) ──
            foreach (var program in validPrograms)
            {
                if (!MeetsEligibility(m, program, completedByCustomer, now, out var basisParts))
                    continue;

                var basis = string.Join(", ", basisParts);
                if (string.IsNullOrWhiteSpace(basis))
                    basis = $"Matches \u201c{program.ProgramName}\u201d eligibility";

                var rewardText = DescribeReward(program);

                recommendations.Add(new RetentionRecommendationDto
                {
                    CustomerId = m.CustomerId,
                    CustomerName = m.FullName,
                    Email = m.Email,
                    Phone = m.Phone,
                    Category = program.RewardType == LoyaltyRewardType.DiscountPercent
                        ? "Discount"
                        : "Reward",
                    Action = program.RewardType == LoyaltyRewardType.DiscountPercent
                        ? "Apply loyalty discount"
                        : "Grant loyalty reward",
                    Basis = basis,
                    LoyaltyProgramId = program.LoyaltyProgramId,
                    ProgramName = program.ProgramName,
                    Reward = rewardText,
                    TransactionCount = m.CompletedTransactions,
                    TotalSpent = m.TotalSpent,
                    DaysSinceLastTransaction = m.DaysSinceLastTransaction,
                    Points = m.LoyaltyPoints ?? 0
                });
            }

            // ── 2. Follow-up on open repair / open interaction ──
            var myOpenRepairs = openRepairs
                .Where(r => r.CustomerId == m.CustomerId)
                .OrderBy(r => r.RequestDate)
                .Take(2)
                .ToList();

            if (myOpenRepairs.Count > 0)
            {
                foreach (var r in myOpenRepairs.Take(1))
                {
                    recommendations.Add(new RetentionRecommendationDto
                    {
                        CustomerId = m.CustomerId,
                        CustomerName = m.FullName,
                        Email = m.Email,
                        Phone = m.Phone,
                        Category = "Follow-up",
                        Action = "Follow up on open repair",
                        Basis = $"Open repair {r.RequestNumber} ({StatusLabel(r.Status)})",
                        TransactionCount = m.CompletedTransactions,
                        TotalSpent = m.TotalSpent,
                        DaysSinceLastTransaction = m.DaysSinceLastTransaction,
                        Points = m.LoyaltyPoints ?? 0
                    });
                }
            }

            var myOpenInteractions = openInteractions
                .Where(i => i.CustomerId == m.CustomerId)
                .ToList();

            if (myOpenInteractions.Count > 0)
            {
                var i0 = myOpenInteractions.OrderByDescending(i => i.InteractionDate).First();
                recommendations.Add(new RetentionRecommendationDto
                {
                    CustomerId = m.CustomerId,
                    CustomerName = m.FullName,
                    Email = m.Email,
                    Phone = m.Phone,
                    Category = "Follow-up",
                    Action = "Resolve open interaction",
                    Basis = $"Open {i0.InteractionType} \u201c{i0.Subject}\u201d ({InteractionLabel(i0.Status)})",
                    TransactionCount = m.CompletedTransactions,
                    TotalSpent = m.TotalSpent,
                    DaysSinceLastTransaction = m.DaysSinceLastTransaction,
                    Points = m.LoyaltyPoints ?? 0
                });
            }

            // ── 3. Re-engagement (inactive customers) ──
            if (m.CompletedTransactions > 0 &&
                m.DaysSinceLastTransaction >= ReEngagementInactiveDays)
            {
                recommendations.Add(new RetentionRecommendationDto
                {
                    CustomerId = m.CustomerId,
                    CustomerName = m.FullName,
                    Email = m.Email,
                    Phone = m.Phone,
                    Category = "Re-engagement",
                    Action = "Win-back outreach",
                    Basis = $"{m.DaysSinceLastTransaction} days since last transaction" +
                            (m.TotalSpent > 0 ? $" \u00b7 \u20b1{m.TotalSpent:N0} total spending" : ""),
                    TransactionCount = m.CompletedTransactions,
                    TotalSpent = m.TotalSpent,
                    DaysSinceLastTransaction = m.DaysSinceLastTransaction,
                    Points = m.LoyaltyPoints ?? 0
                });
            }
            else if (m.CompletedTransactions == 0 &&
                     m.DaysSinceLastTransaction >= ChurnInactiveDays)
            {
                recommendations.Add(new RetentionRecommendationDto
                {
                    CustomerId = m.CustomerId,
                    CustomerName = m.FullName,
                    Email = m.Email,
                    Phone = m.Phone,
                    Category = "Re-engagement",
                    Action = "First-purchase incentive",
                    Basis = $"No transactions yet \u00b7 customer for {m.DaysSinceLastTransaction} days",
                    TransactionCount = m.CompletedTransactions,
                    TotalSpent = m.TotalSpent,
                    DaysSinceLastTransaction = m.DaysSinceLastTransaction,
                    Points = m.LoyaltyPoints ?? 0
                });
            }
        }

        // ── Filtering ──
        IEnumerable<RetentionRecommendationDto> result = recommendations;

        if (filter?.Category is { Length: > 0 } cat && cat != "All")
            result = result.Where(r => r.Category.Equals(cat, StringComparison.OrdinalIgnoreCase));

        if (filter?.MinSpent > 0)
            result = result.Where(r => r.TotalSpent >= filter.MinSpent);

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var term = filter.Search.Trim();
            result = result.Where(r =>
                r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var order = new[] { "Reward", "Discount", "Follow-up", "Re-engagement" };
        return result
            .OrderBy(r => Array.IndexOf(order, r.Category))
            .ThenByDescending(r => r.TotalSpent)
            .ToList();
    }

    private static bool MeetsEligibility(
        CustomerRetentionMetricsDto m,
        LoyaltyProgram program,
        Dictionary<int, List<RepairRequest>> completedByCustomer,
        DateTime now,
        out List<string> basisParts)
    {
        basisParts = new List<string>();

        if (program.MinTransactions.HasValue && m.CompletedTransactions >= program.MinTransactions.Value)
            basisParts.Add($"{m.CompletedTransactions} completed transactions");
        else if (program.MinTransactions.HasValue)
            return false;

        if (program.MinTotalSpent.HasValue && m.TotalSpent >= program.MinTotalSpent.Value)
            basisParts.Add($"\u20b1{m.TotalSpent:N0} total spending");
        else if (program.MinTotalSpent.HasValue)
            return false;

        if (program.MaxInactiveDays.HasValue)
        {
            if (m.DaysSinceLastTransaction > program.MaxInactiveDays.Value)
                return false;
            if (m.CompletedTransactions > 0)
                basisParts.Add($"{m.DaysSinceLastTransaction} days since last transaction");
        }

        if (program.MinVisitsPerPeriod.HasValue && program.VisitPeriodDays.HasValue)
        {
            // Visit frequency measured over the configured window using real history.
            var windowStart = now.AddDays(-program.VisitPeriodDays.Value);
            var visits = completedByCustomer.TryGetValue(m.CustomerId, out var repairs)
                ? repairs.Count(r => r.CompletionDate!.Value >= windowStart)
                : 0;
            if (visits < program.MinVisitsPerPeriod.Value)
                return false;
            basisParts.Add($"{visits} visits in the last {program.VisitPeriodDays.Value} days");
        }

        if (program.MinimumSpend > 0 && m.TotalSpent < program.MinimumSpend)
            return false;

        return true;
    }

    private static string DescribeReward(LoyaltyProgram program)
    {
        var value = program.RewardValue;
        return program.RewardType switch
        {
            LoyaltyRewardType.DiscountPercent =>
                $"{value:0.#}% discount off next service",
            LoyaltyRewardType.FreeService =>
                $"Free service \u2013 up to \u20b1{value:N0}",
            LoyaltyRewardType.PointsMultiplier =>
                $"{value:0.#}\u00d7 points multiplier",
            LoyaltyRewardType.Voucher =>
                $"\u20b1{value:N0} voucher",
            _ => program.ProgramName
        };
    }

    private static string StatusLabel(RepairStatus status) => status switch
    {
        RepairStatus.Pending => "Pending",
        RepairStatus.Approved => "Approved",
        RepairStatus.InProgress => "In Progress",
        RepairStatus.Reassigned => "Reassigned",
        _ => status.ToString()
    };

    private static string InteractionLabel(InteractionStatus status) => status switch
    {
        InteractionStatus.Open => "Open",
        InteractionStatus.InProgress => "In Progress",
        InteractionStatus.Closed => "Closed",
        _ => status.ToString()
    };
}

public class RevenueFilter
{
    public string? Category { get; set; }
    public decimal MinSpent { get; set; }
    public string? Search { get; set; }
}