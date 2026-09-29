using System;
using System.Collections.Generic;

namespace CRM.winforms
{
    public class LoyaltyProgramDto
    {
        public int LoyaltyProgramId { get; set; }
        public int? CompanyId { get; set; }
        public string ProgramName { get; set; } = "";
        public string Description { get; set; } = "";
        public int PointsPerPeso { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal MinimumSpend { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public int? PointsValidityDays { get; set; }
        public int? RedeemPointsRequired { get; set; }

        // Eligibility criteria — evaluated against real customer history
        public int? MinTransactions { get; set; }
        public decimal? MinTotalSpent { get; set; }
        public int? MaxInactiveDays { get; set; }
        public int? MinVisitsPerPeriod { get; set; }
        public int? VisitPeriodDays { get; set; }

        // Reward definition: 0 DiscountPercent, 1 FreeService, 2 PointsMultiplier, 3 Voucher
        public int RewardType { get; set; }
        public decimal RewardValue { get; set; }
        public int? MaxRedemptionsPerCustomer { get; set; }

        public string StatusText => IsActive ? "Active" : "Archived";
        public string PointsDisplay => $"{PointsPerPeso} pt / ₱1";
        public string DiscountDisplay => $"{DiscountPercentage:0.#}%";
        public string MinSpendDisplay => $"₱{MinimumSpend:N2}";
        public string StartDateDisplay => StartDate.ToString("MMM d, yyyy");
        public string EndDateDisplay => EndDate.ToString("MMM d, yyyy");
        public string RewardTypeText => RewardType switch
        {
            0 => "Discount %",
            1 => "Free service",
            2 => "Points multiplier",
            3 => "Voucher",
            _ => "—"
        };
        public string RewardDisplay => RewardType switch
        {
            0 => $"{RewardValue:0.#}% off",
            1 => $"Free svc ≤₱{RewardValue:N0}",
            2 => $"{RewardValue:0.#}× pts",
            3 => $"₱{RewardValue:N0} voucher",
            _ => "—"
        };
        public string EligibilityText
        {
            get
            {
                var parts = new List<string>();
                if (MinTransactions.HasValue) parts.Add($"{MinTransactions}+ tx");
                if (MinTotalSpent.HasValue) parts.Add($"₱{MinTotalSpent:N0}+ spent");
                if (MaxInactiveDays.HasValue) parts.Add($"≤{MaxInactiveDays}d idle");
                if (MinVisitsPerPeriod.HasValue && VisitPeriodDays.HasValue)
                    parts.Add($"{MinVisitsPerPeriod}+ visits/{VisitPeriodDays}d");
                if (MinimumSpend > 0) parts.Add($"min spend ₱{MinimumSpend:N0}");
                return parts.Count > 0 ? string.Join(" · ", parts) : "All customers";
            }
        }
    }

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
}
