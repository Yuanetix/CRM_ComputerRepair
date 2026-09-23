namespace CRM_ComputerRepair.api.Dtos;

// ═══════════════════════════════════════════════════════════════
//  Dashboard / Business Intelligence
// ═══════════════════════════════════════════════════════════════

public class DashboardResponse
{
    // Customers
    public int TotalCustomers { get; set; }
    public int ActiveCustomers { get; set; }
    public int InactiveCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public int NewThisMonth { get; set; }

    // Transactions & sales
    public int TotalTransactions { get; set; }
    public decimal TotalSales { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public int TransactionsThisMonth { get; set; }

    // Loyalty participation
    public int LoyaltyMembers { get; set; }
    public double LoyaltyParticipationRate { get; set; }

    // Visit frequency (avg completed transactions per customer with 1+)
    public double AverageVisitsPerCustomer { get; set; }

    // Customers with no completed transaction in the last 90 days
    public int Inactive90Days { get; set; }

    // Retention
    public double RetentionRate { get; set; }
    public double RepeatCustomerRate { get; set; }
    public int ChurnRisk { get; set; }
    public int OpenInteractions { get; set; }

    // Operations
    public int RepairsCompletedThisMonth { get; set; }
    public double AverageTurnaroundDays { get; set; }

    // Customer activity (last 30 days: interactions + repair activity)
    public int CustomerActivityLast30Days { get; set; }

    public ServiceStatDto TopService { get; set; } = new();

    public InteractionsByTypeDto InteractionsByType { get; set; } = new();
    public RepairsByStatusDto RepairsByStatus { get; set; } = new();

    public List<TimeSeriesPointDto> CustomersOverTime { get; set; } = new();
    public List<SalesPointDto> SalesOverTime { get; set; } = new();
    public List<TimeSeriesPointDto> TransactionsOverTime { get; set; } = new();
    public List<PopularServiceDto> PopularServices { get; set; } = new();
    public List<ActivityPointDto> CustomerActivityTrend { get; set; } = new();
    public List<RetentionTrendPointDto> RetentionTrend { get; set; } = new();
    public List<LoyaltyPerformanceDto> LoyaltyPerformance { get; set; } = new();
}

public class ServiceStatDto
{
    public string Name { get; set; } = "—";
    public int Count { get; set; }
    public decimal Revenue { get; set; }
}

public class InteractionsByTypeDto
{
    public int Inquiry { get; set; }
    public int Complaint { get; set; }
    public int Feedback { get; set; }
}

public class RepairsByStatusDto
{
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public int Reassigned { get; set; }
}

public class TimeSeriesPointDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public class SalesPointDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Sales { get; set; }
}

public class PopularServiceDto
{
    public string Service { get; set; } = "";
    public int Count { get; set; }
    public decimal Revenue { get; set; }
}

public class ActivityPointDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public int Interactions { get; set; }
    public int Repairs { get; set; }
}

public class RetentionTrendPointDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public int Active { get; set; }
}

public class LoyaltyPerformanceDto
{
    public int LoyaltyProgramId { get; set; }
    public string ProgramName { get; set; } = "";
    public int Members { get; set; }
    public int TotalPoints { get; set; }
    public decimal TotalSpent { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Per-customer visit frequency (drill-down for the visit-frequency KPI).</summary>
public class CustomerVisitDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int VisitCount { get; set; }
    public DateTime? FirstVisit { get; set; }
    public DateTime? LastVisit { get; set; }
    public double VisitsPerMonth { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>Actual loyalty member record (drill-down for loyalty KPIs/charts).</summary>
public class LoyaltyMemberDetailDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public int Points { get; set; }
    public DateTime JoinedDate { get; set; }
    public decimal TotalSpent { get; set; }
    public bool IsActive { get; set; }
}

// ═══════════════════════════════════════════════════════════════
//  Drill-down detail — generic columns + rows for the WinForms grid
// ═══════════════════════════════════════════════════════════════

public class DetailColumnDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text"; // text | number | currency | date
}

public class AnalyticsDetailsResponse
{
    public string Metric { get; set; } = "";
    public string? Title { get; set; }
    public List<DetailColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}