using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/analytics")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly MasterCrmDbContext _master;

    public AnalyticsController(ITenantDbContextFactory factory, MasterCrmDbContext master)
    {
        _factory = factory;
        _master = master;
    }

    /// <summary>Full dashboard: KPIs, trends, charts and loyalty performance computed from real records.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sixMonthsAgo = startOfMonth.AddMonths(-6);

        var customers = await db.Customers.AsNoTracking().ToListAsync();
        var repairs = await db.RepairRequests.AsNoTracking().ToListAsync();
        var payments = await db.Payments.AsNoTracking().Where(p => !p.IsVoid).ToListAsync();
        var interactions = await db.CustomerInteractions.AsNoTracking().ToListAsync();

        var completedRepairs = repairs
            .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
            .ToList();

        var paymentByRepair = payments
            .Where(p => p.IsPaid)
            .GroupBy(p => p.RepairRequestId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var repairsByCustomer = repairs
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count());

        var completedByCustomer = completedRepairs
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count());

        var returningCustomers = completedByCustomer.Count(kv => kv.Value >= 2);
        var customersWithRepairs = completedByCustomer.Count;
        var totalTransactions = completedRepairs.Count;
        var totalSales = payments.Where(p => p.IsPaid).Sum(p => p.Amount);
        var averageTransactionValue = totalSales > 0 && payments.Count(p => p.IsPaid) > 0
            ? Math.Round(totalSales / payments.Count(p => p.IsPaid), 2)
            : 0m;

        var transactionsThisMonth = completedRepairs.Count(r => r.CompletionDate!.Value >= startOfMonth);
        var completedThisMonth = transactionsThisMonth;
        var newThisMonth = customers.Count(c => c.CreatedAt >= startOfMonth);

        var avgTurnaround = completedRepairs.Count > 0
            ? Math.Round(completedRepairs.Average(r =>
                (r.CompletionDate!.Value - r.RequestDate).TotalDays), 1)
            : 0.0;

        var retentionRate = customers.Count > 0
            ? Math.Round(100.0 * returningCustomers / customers.Count, 1)
            : 0.0;

        var repeatCustomerRate = customersWithRepairs > 0
            ? Math.Round(100.0 * returningCustomers / customersWithRepairs, 1)
            : 0.0;

        var openInteractions = interactions.Count(i => i.Status != InteractionStatus.Closed);

        var latelyActiveIds = new HashSet<int>();
        foreach (var i in interactions.Where(i => i.InteractionDate >= now.AddDays(-90) && i.CustomerId.HasValue))
            latelyActiveIds.Add(i.CustomerId!.Value);
        foreach (var r in completedRepairs.Where(r => r.CompletionDate!.Value >= now.AddDays(-90)))
            latelyActiveIds.Add(r.CustomerId);

        var churnRisk = customers.Count(c =>
            c.IsActive && !latelyActiveIds.Contains(c.CustomerId));

        var activity30Ids = new HashSet<int>();
        foreach (var i in interactions.Where(i => i.InteractionDate >= now.AddDays(-30) && i.CustomerId.HasValue))
            activity30Ids.Add(i.CustomerId!.Value);
        foreach (var r in repairs.Where(r => r.RequestDate >= now.AddDays(-30)))
            activity30Ids.Add(r.CustomerId);

        // ── Visit frequency per customer (completed transactions) ──
        var visitsByCustomer = completedRepairs
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                Last = g.Max(r => r.CompletionDate)!.Value
            });

        var avgVisits = visitsByCustomer.Count > 0
            ? Math.Round(visitsByCustomer.Values.Average(v => (double)v.Count), 1)
            : 0.0;

        // Inactive 90 days: active customers with no completed transaction in 90 days
        var inactive90 = customers.Count(c => c.IsActive
            && (!visitsByCustomer.TryGetValue(c.CustomerId, out var vv) || vv.Last < now.AddDays(-90)));

        // ── Loyalty participation (master programs × tenant customers) ──
        var tenantCustomerIds = customers.Select(c => c.CustomerId).ToList();
        var loyaltyAccounts = await _master.CustomerLoyaltyAccounts.AsNoTracking()
            .Where(a => tenantCustomerIds.Contains(a.CustomerId))
            .ToListAsync();

        var loyaltyMembers = loyaltyAccounts.Where(a => a.IsActive)
            .Select(a => a.CustomerId).Distinct().Count();

        var loyaltyParticipationRate = customers.Count > 0
            ? Math.Round(100.0 * loyaltyMembers / customers.Count, 1)
            : 0.0;

        // ── Time series (6 months) ──
        var customersOverTime = new List<TimeSeriesPointDto>();
        var salesOverTime = new List<SalesPointDto>();
        var transactionsOverTime = new List<TimeSeriesPointDto>();
        var customerActivityTrend = new List<ActivityPointDto>();
        var retentionTrend = new List<RetentionTrendPointDto>();

        for (int i = 5; i >= 0; i--)
        {
            var monthStart = startOfMonth.AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var label = monthStart.ToString("MMM yyyy");

            customersOverTime.Add(new TimeSeriesPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = label,
                Count = customers.Count(c => c.CreatedAt >= monthStart && c.CreatedAt < monthEnd)
            });

            salesOverTime.Add(new SalesPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = label,
                Sales = payments.Where(p => p.IsPaid && p.PaymentDate >= monthStart && p.PaymentDate < monthEnd)
                    .Sum(p => p.Amount)
            });

            transactionsOverTime.Add(new TimeSeriesPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = label,
                Count = completedRepairs.Count(r =>
                    r.CompletionDate!.Value >= monthStart && r.CompletionDate!.Value < monthEnd)
            });

            customerActivityTrend.Add(new ActivityPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = label,
                Interactions = interactions.Count(i => i.InteractionDate >= monthStart && i.InteractionDate < monthEnd),
                Repairs = repairs.Count(r => r.RequestDate >= monthStart && r.RequestDate < monthEnd)
            });

            retentionTrend.Add(new RetentionTrendPointDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Label = label,
                Active = interactions
                    .Where(i => i.InteractionDate >= monthStart && i.InteractionDate < monthEnd && i.CustomerId.HasValue)
                    .Select(i => i.CustomerId!.Value).Distinct().Count()
            });
        }

        // ── Popular services (by DeviceModel) ──
        var popularServices = repairs
            .Where(r => !string.IsNullOrWhiteSpace(r.DeviceModel))
            .GroupBy(r => r.DeviceModel.Trim())
            .Select(g => new PopularServiceDto
            {
                Service = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(r =>
                    paymentByRepair.TryGetValue(r.RepairRequestId, out var amt) ? amt : 0m)
            })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        var topService = popularServices.FirstOrDefault();

        var topServiceStat = topService is null
            ? new ServiceStatDto()
            : new ServiceStatDto
            {
                Name = topService.Service,
                Count = topService.Count,
                Revenue = topService.Revenue
            };

        // ── Loyalty performance (master programs × tenant spend) ──
        var activePrograms = await _master.LoyaltyPrograms.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProgramName)
            .ToListAsync();

        var tenantIds = customers.Select(c => c.CustomerId).ToList();

        var loyalAccounts = await _master.CustomerLoyaltyAccounts.AsNoTracking()
            .Where(a => tenantIds.Contains(a.CustomerId))
            .ToListAsync();

        var spendByCustomer = payments
            .Where(p => p.IsPaid)
            .Join(repairs, p => p.RepairRequestId, r => r.RepairRequestId, (p, r) => new { p, cid = r.CustomerId })
            .GroupBy(x => x.cid)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.p.Amount));

        var loyaltyPerformance = activePrograms.Select(p =>
        {
            var members = loyalAccounts.Where(a => a.LoyaltyProgramId == p.LoyaltyProgramId).ToList();
            return new LoyaltyPerformanceDto
            {
                LoyaltyProgramId = p.LoyaltyProgramId,
                ProgramName = p.ProgramName,
                Members = members.Count,
                TotalPoints = members.Sum(a => a.Points),
                TotalSpent = members.Sum(a =>
                    spendByCustomer.TryGetValue(a.CustomerId, out var amt) ? amt : 0m),
                IsActive = true
            };
        }).ToList();

        var response = new DashboardResponse
        {
            TotalCustomers = customers.Count,
            ActiveCustomers = customers.Count(c => c.IsActive),
            InactiveCustomers = customers.Count(c => !c.IsActive),
            ReturningCustomers = returningCustomers,
            NewThisMonth = newThisMonth,
            TotalTransactions = totalTransactions,
            TotalSales = totalSales,
            AverageTransactionValue = averageTransactionValue,
            TransactionsThisMonth = transactionsThisMonth,
            LoyaltyMembers = loyaltyMembers,
            LoyaltyParticipationRate = loyaltyParticipationRate,
            AverageVisitsPerCustomer = avgVisits,
            Inactive90Days = inactive90,
            RetentionRate = retentionRate,
            RepeatCustomerRate = repeatCustomerRate,
            ChurnRisk = churnRisk,
            OpenInteractions = openInteractions,
            RepairsCompletedThisMonth = completedThisMonth,
            AverageTurnaroundDays = avgTurnaround,
            CustomerActivityLast30Days = activity30Ids.Count,
            TopService = topServiceStat,
            InteractionsByType = new InteractionsByTypeDto
            {
                Inquiry = interactions.Count(i => i.InteractionType == InteractionType.Inquiry),
                Complaint = interactions.Count(i => i.InteractionType == InteractionType.Complaint),
                Feedback = interactions.Count(i => i.InteractionType == InteractionType.Feedback)
            },
            RepairsByStatus = new RepairsByStatusDto
            {
                Pending = repairs.Count(r => r.Status == RepairStatus.Pending),
                Approved = repairs.Count(r => r.Status == RepairStatus.Approved),
                InProgress = repairs.Count(r => r.Status == RepairStatus.InProgress),
                Completed = repairs.Count(r => r.Status == RepairStatus.Completed),
                Rejected = repairs.Count(r => r.Status == RepairStatus.Rejected),
                Reassigned = repairs.Count(r => r.Status == RepairStatus.Reassigned)
            },
            CustomersOverTime = customersOverTime,
            SalesOverTime = salesOverTime,
            TransactionsOverTime = transactionsOverTime,
            PopularServices = popularServices,
            CustomerActivityTrend = customerActivityTrend,
            RetentionTrend = retentionTrend,
            LoyaltyPerformance = loyaltyPerformance
        };

        return Ok(response);
    }

    // ═══════════════════════════════════════════════════════════
    //  Generic drill-down
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a uniform {columns, rows} shape for any dashboard metric so the
    /// WinForms drill-down grid can render it generically. Filters apply where
    /// relevant to the metric.
    /// </summary>
    [HttpGet("details")]
    public async Task<IActionResult> Details(
        int companyId,
        [FromQuery] string metric,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        [FromQuery] string? service,
        [FromQuery] string? status)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var now = DateTime.UtcNow;
        var fromUtc = from?.ToUniversalTime();
        var toUtc = to?.ToUniversalTime();

        switch ((metric ?? "").ToLowerInvariant())
        {
            case "customers":
            {
                var customers = await db.Customers.AsNoTracking()
                    .Where(c => !customerId.HasValue || c.CustomerId == customerId.Value)
                    .Where(c => !fromUtc.HasValue || c.CreatedAt >= fromUtc.Value)
                    .Where(c => !toUtc.HasValue || c.CreatedAt <= toUtc.Value)
                    .ToListAsync();

                var repairs = await db.RepairRequests.AsNoTracking()
                    .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
                    .ToListAsync();

                var payments = await db.Payments.AsNoTracking()
                    .Where(p => p.IsPaid && !p.IsVoid)
                    .ToListAsync();

                var byRepair = payments
                    .GroupBy(p => p.RepairRequestId)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

                var rows = new List<Dictionary<string, object?>>();
                foreach (var c in customers.OrderByDescending(c => c.CreatedAt))
                {
                    var custRepairs = repairs.Where(r => r.CustomerId == c.CustomerId).ToList();
                    var spent = custRepairs
                        .Sum(r => byRepair.TryGetValue(r.RepairRequestId, out var amt) ? amt : 0m);
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["customerId"] = c.CustomerId,
                        ["name"] = $"{c.FirstName} {c.LastName}".Trim(),
                        ["email"] = c.Email,
                        ["phone"] = c.Phone,
                        ["status"] = c.IsActive ? "Active" : "Inactive",
                        ["created"] = c.CreatedAt,
                        ["transactions"] = custRepairs.Count,
                        ["totalSpent"] = spent,
                        ["loyaltyPoints"] = c.LoyaltyPoints ?? 0
                    });
                }

                return Ok(new AnalyticsDetailsResponse
                {
                    Metric = "customers",
                    Columns = new List<DetailColumnDto>
                    {
                        Col("name", "Customer", "text"),
                        Col("email", "Email", "text"),
                        Col("phone", "Phone", "text"),
                        Col("status", "Status", "text"),
                        Col("created", "Created", "date"),
                        Col("transactions", "Transactions", "number"),
                        Col("totalSpent", "Total Spent", "currency"),
                        Col("loyaltyPoints", "Loyalty Pts", "number")
                    },
                    Rows = rows
                });
            }

            case "transactions":
            {
                var repairs = await db.RepairRequests.AsNoTracking()
                    .Where(r => !customerId.HasValue || r.CustomerId == customerId.Value)
                    .Where(r => !fromUtc.HasValue || r.RequestDate >= fromUtc.Value)
                    .Where(r => !toUtc.HasValue || r.RequestDate <= toUtc.Value)
                    .Where(r => string.IsNullOrWhiteSpace(service) ||
                                r.DeviceModel.Contains(service.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Where(r => string.IsNullOrWhiteSpace(status) ||
                                r.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
                    .ToListAsync();

                var customers = await db.Customers.AsNoTracking()
                    .ToDictionaryAsync(c => c.CustomerId, c => $"{c.FirstName} {c.LastName}".Trim());

                var payments = await db.Payments.AsNoTracking()
                    .Where(p => p.IsPaid && !p.IsVoid)
                    .GroupBy(p => p.RepairRequestId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.Amount));

                var rows = repairs
                    .OrderByDescending(r => r.RequestDate)
                    .Select(r =>
                    {
                        var row = new Dictionary<string, object?>
                        {
                            ["requestNumber"] = r.RequestNumber,
                            ["customer"] = customers.TryGetValue(r.CustomerId, out var n) ? n : "-",
                            ["service"] = r.DeviceModel,
                            ["requested"] = r.RequestDate,
                            ["completed"] = r.CompletionDate,
                            ["status"] = r.Status.ToString(),
                            ["amount"] = payments.TryGetValue(r.RepairRequestId, out var amt) ? amt : 0m
                        };
                        return row;
                    })
                    .ToList();

                return Ok(new AnalyticsDetailsResponse
                {
                    Metric = "transactions",
                    Columns = new List<DetailColumnDto>
                    {
                        Col("requestNumber", "Request #", "text"),
                        Col("customer", "Customer", "text"),
                        Col("service", "Service", "text"),
                        Col("requested", "Requested", "date"),
                        Col("completed", "Completed", "date"),
                        Col("status", "Status", "text"),
                        Col("amount", "Amount", "currency")
                    },
                    Rows = rows
                });
            }

            case "sales":
            {
                var payments = await db.Payments.AsNoTracking()
                    .Where(p => p.IsPaid && !p.IsVoid)
                    .Where(p => !fromUtc.HasValue || p.PaymentDate >= fromUtc.Value)
                    .Where(p => !toUtc.HasValue || p.PaymentDate <= toUtc.Value)
                    .ToListAsync();

                var repairs = await db.RepairRequests.AsNoTracking()
                    .ToDictionaryAsync(r => r.RepairRequestId, r => r);

                var customers = await db.Customers.AsNoTracking()
                    .ToDictionaryAsync(c => c.CustomerId, c => $"{c.FirstName} {c.LastName}".Trim());

                var rows = payments
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p =>
                    {
                        repairs.TryGetValue(p.RepairRequestId, out var r);
                        var row = new Dictionary<string, object?>
                        {
                            ["paymentId"] = p.PaymentId,
                            ["requestNumber"] = r?.RequestNumber ?? "-",
                            ["customer"] = r != null && customers.TryGetValue(r.CustomerId, out var n) ? n : "-",
                            ["service"] = r?.DeviceModel ?? "-",
                            ["paidOn"] = p.PaymentDate,
                            ["method"] = p.PaymentMethod ?? "-",
                            ["reference"] = p.ReferenceNumber ?? "",
                            ["amount"] = p.Amount
                        };
                        return row;
                    })
                    .ToList();

                return Ok(new AnalyticsDetailsResponse
                {
                    Metric = "sales",
                    Columns = new List<DetailColumnDto>
                    {
                        Col("requestNumber", "Request #", "text"),
                        Col("customer", "Customer", "text"),
                        Col("service", "Service", "text"),
                        Col("paidOn", "Paid On", "date"),
                        Col("method", "Method", "text"),
                        Col("reference", "Reference", "text"),
                        Col("amount", "Amount", "currency")
                    },
                    Rows = rows
                });
            }

            case "services":
            {
                var repairs = await db.RepairRequests.AsNoTracking().ToListAsync();
                var payments = await db.Payments.AsNoTracking()
                    .Where(p => p.IsPaid && !p.IsVoid)
                    .GroupBy(p => p.RepairRequestId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.Amount));

                var byService = repairs
                    .Where(r => !string.IsNullOrWhiteSpace(r.DeviceModel))
                    .GroupBy(r => r.DeviceModel.Trim())
                    .Select(g => new
                    {
                        Service = g.Key,
                        Count = g.Count(),
                        Revenue = g.Sum(r =>
                            payments.TryGetValue(r.RepairRequestId, out var amt) ? amt : 0m),
                        LastDate = g.Max(r => r.RequestDate)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var rows = new List<Dictionary<string, object?>>();
                foreach (var s in byService)
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["service"] = s.Service,
                        ["count"] = s.Count,
                        ["revenue"] = s.Revenue,
                        ["avgValue"] = s.Count > 0 ? Math.Round(s.Revenue / s.Count, 2) : 0m,
                        ["lastRequest"] = s.LastDate
                    });
                }

                return Ok(new AnalyticsDetailsResponse
                {
                    Metric = "services",
                    Columns = new List<DetailColumnDto>
                    {
                        Col("service", "Service", "text"),
                        Col("count", "Requests", "number"),
                        Col("revenue", "Revenue", "currency"),
                        Col("avgValue", "Avg Value", "currency"),
                        Col("lastRequest", "Last Request", "date")
                    },
                    Rows = rows
                });
            }

            case "interactions":
            {
                var interactions = await db.CustomerInteractions.AsNoTracking()
                    .Where(i => !customerId.HasValue || i.CustomerId == customerId.Value)
                    .Where(i => !fromUtc.HasValue || i.InteractionDate >= fromUtc.Value)
                    .Where(i => !toUtc.HasValue || i.InteractionDate <= toUtc.Value)
                    .OrderByDescending(i => i.InteractionDate)
                    .ToListAsync();

                var customers = await db.Customers.AsNoTracking()
                    .ToDictionaryAsync(c => c.CustomerId, c => $"{c.FirstName} {c.LastName}".Trim());

                var rows = new List<Dictionary<string, object?>>();
                foreach (var i in interactions)
                {
                    var customerName = "-";
                    if (i.CustomerId.HasValue &&
                        customers.TryGetValue(i.CustomerId.Value, out var cname))
                        customerName = cname;

                    rows.Add(new Dictionary<string, object?>
                    {
                        ["date"] = i.InteractionDate,
                        ["customer"] = customerName,
                        ["type"] = i.InteractionType.ToString(),
                        ["status"] = i.Status.ToString(),
                        ["subject"] = i.Subject,
                        ["priority"] = i.Priority.ToString()
                    });
                }

                return Ok(new AnalyticsDetailsResponse
                {
                    Metric = "interactions",
                    Columns = new List<DetailColumnDto>
                    {
                        Col("date", "Date", "date"),
                        Col("customer", "Customer", "text"),
                        Col("type", "Type", "text"),
                        Col("priority", "Priority", "text"),
                        Col("status", "Status", "text"),
                        Col("subject", "Subject", "text")
                    },
                    Rows = rows
                });
            }

            case "loyalty":
            {
                var customerIds = await db.Customers.AsNoTracking()
                    .Select(c => c.CustomerId)
                    .ToListAsync();

                var activePrograms = await _master.LoyaltyPrograms.AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.ProgramName)
                    .ToListAsync();

                var accounts = await _master.CustomerLoyaltyAccounts.AsNoTracking()
                    .Where(a => customerIds.Contains(a.CustomerId))
                    .ToListAsync();

                var rows = new List<Dictionary<string, object?>>();
                foreach (var p in activePrograms)
                {
                    var members = accounts.Where(a => a.LoyaltyProgramId == p.LoyaltyProgramId).ToList();
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["program"] = p.ProgramName,
                        ["members"] = members.Count,
                        ["totalPoints"] = members.Sum(a => a.Points),
                        ["redeemPointsRequired"] = p.RedeemPointsRequired ?? 0,
                        ["rewardType"] = p.RewardType.ToString(),
                        ["rewardValue"] = p.RewardValue,
                        ["eligibleCustomers"] = p.MinTransactions.HasValue
                            ? $"min {p.MinTransactions} tx"
                            : p.MinTotalSpent.HasValue
                                ? $"min \u20b1{p.MinTotalSpent:N0}"
                                : "any"
                    });
                }

                return Ok(new AnalyticsDetailsResponse
                {
                    Metric = "loyalty",
                    Columns = new List<DetailColumnDto>
                    {
                        Col("program", "Program", "text"),
                        Col("members", "Members", "number"),
                        Col("totalPoints", "Total Points", "number"),
                        Col("redeemPointsRequired", "Redeem Points", "number"),
                        Col("rewardType", "Reward Type", "text"),
                        Col("rewardValue", "Reward Value", "currency"),
                        Col("eligibleCustomers", "Eligibility Basis", "text")
                    },
                    Rows = rows
                });
            }

            default:
                return BadRequest(new { message = $"Unknown metric '{metric}'. " +
                    "Supported: customers, transactions, sales, services, interactions, loyalty." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Targeted drill-downs (KPI cards & chart points)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Per-customer visit frequency computed from completed transactions.</summary>
    [HttpGet("visits")]
    public async Task<IActionResult> Visits(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var completed = await db.RepairRequests.AsNoTracking()
            .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
            .ToListAsync();

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.IsPaid && !p.IsVoid)
            .ToListAsync();

        var customers = await db.Customers.AsNoTracking().ToListAsync();

        var paymentsByRepair = payments.GroupBy(p => p.RepairRequestId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var rows = completed
            .GroupBy(r => r.CustomerId)
            .Select(g =>
            {
                var c = customers.FirstOrDefault(x => x.CustomerId == g.Key);
                var spent = g.Sum(r => paymentsByRepair.TryGetValue(r.RepairRequestId, out var amt) ? amt : 0m);
                var first = g.Min(r => r.CompletionDate)!.Value;
                var last = g.Max(r => r.CompletionDate)!.Value;
                var months = Math.Max(1.0, (last - first).TotalDays / 30.44);

                return new CustomerVisitDto
                {
                    CustomerId = g.Key,
                    CustomerName = c is null ? $"(customer {g.Key})" : $"{c.FirstName} {c.LastName}".Trim(),
                    Email = c?.Email,
                    Phone = c?.Phone,
                    VisitCount = g.Count(),
                    FirstVisit = first,
                    LastVisit = last,
                    VisitsPerMonth = Math.Round(g.Count() / months, 2),
                    TotalSpent = spent
                };
            })
            .OrderByDescending(v => v.VisitCount)
        .ToList();

        return Ok(rows);
    }

    /// <summary>Active customers with no completed transaction in the last 90 days.</summary>
    [HttpGet("inactive-customers")]
    public async Task<IActionResult> InactiveCustomers(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var now = DateTime.UtcNow;
        var customers = await db.Customers.AsNoTracking().Where(c => c.IsActive).ToListAsync();

        var completed = await db.RepairRequests.AsNoTracking()
            .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
            .ToListAsync();

        var lastVisit = completed.GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.CompletionDate)!.Value);

        var cutoff = now.AddDays(-90);

        var rows = customers
            .Where(c => !lastVisit.TryGetValue(c.CustomerId, out var lv) || lv < cutoff)

            .Select(c => new Dictionary<string, object?>
            {
                ["customerId"] = c.CustomerId,
                ["name"] = $"{c.FirstName} {c.LastName}".Trim(),
                ["email"] = c.Email,
                ["phone"] = c.Phone,
                ["lastVisit"] = lastVisit.TryGetValue(c.CustomerId, out var lv) ? lv : (object?)null,
                ["inactiveDays"] = lastVisit.TryGetValue(c.CustomerId, out var lv2)
                    ? (int)(now - lv2).TotalDays
                    : (int)(now - c.CreatedAt).TotalDays,
                ["since"] = c.CreatedAt
            })
            .OrderByDescending(r => r["inactiveDays"])
            .ToList();

        return Ok(new AnalyticsDetailsResponse
        {
            Metric = "inactive-customers",
            Title = $"Inactive customers — no completed transaction since {cutoff:MMM d, yyyy}",
            Columns = new List<DetailColumnDto>
            {
                Col("name", "Customer", "text"),
                Col("email", "Email", "text"),
                Col("phone", "Phone", "text"),
                Col("lastVisit", "Last Visit", "date"),
                Col("inactiveDays", "Days Inactive", "number"),
                Col("since", "Customer Since", "date")
            },
            Rows = rows
        });
    }

    /// <summary>Loyalty members joined with tenant customer names and real spend.</summary>
    [HttpGet("loyalty-members")]
    public async Task<IActionResult> LoyaltyMembers(
        int companyId, [FromQuery] int? programId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var customerIds = await db.Customers.AsNoTracking()
            .Select(c => c.CustomerId).ToListAsync();

        var accounts = await _master.CustomerLoyaltyAccounts.AsNoTracking()
            .Where(a => customerIds.Contains(a.CustomerId))
            .Where(a => !programId.HasValue || a.LoyaltyProgramId == programId.Value)
            .ToListAsync();

        var programs = await _master.LoyaltyPrograms.AsNoTracking().ToListAsync();
        var programNames = programs.ToDictionary(p => p.LoyaltyProgramId, p => p.ProgramName);

        var customers = await db.Customers.AsNoTracking().ToListAsync();
        var byCustomer = customers.ToDictionary(c => c.CustomerId);

        var payments = await db.Payments.AsNoTracking().Where(p => p.IsPaid && !p.IsVoid).ToListAsync();
        var repairs = await db.RepairRequests.AsNoTracking().ToListAsync();

        var spentByCustomer = payments
            .Join(repairs, p => p.RepairRequestId, r => r.RepairRequestId,
                (p, r) => new { p.Amount, r.CustomerId })
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var rows = accounts
            .Select(a =>
            {
                byCustomer.TryGetValue(a.CustomerId, out var c);
                return new LoyaltyMemberDetailDto
                {
                    CustomerId = a.CustomerId,
                    CustomerName = c is null ? $"(customer {a.CustomerId})" : $"{c.FirstName} {c.LastName}".Trim(),
                    ProgramName = programNames.TryGetValue(a.LoyaltyProgramId, out var n) ? n : $"Program {a.LoyaltyProgramId}",
                    Points = a.Points,
                    JoinedDate = a.JoinedDate,
                    TotalSpent = spentByCustomer.TryGetValue(a.CustomerId, out var s) ? s : 0m,
                    IsActive = a.IsActive
                };
            })
            .OrderByDescending(m => m.TotalSpent)
            .ToList();

        return Ok(rows);
    }

    private static DetailColumnDto Col(string key, string label, string type) =>
        new() { Key = key, Label = label, Type = type };
}