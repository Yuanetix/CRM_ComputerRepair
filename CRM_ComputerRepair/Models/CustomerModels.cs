using System;
using System.Collections.Generic;

namespace CRM.winforms
{
    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public int? LoyaltyPoints { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Address)) parts.Add(Address.Trim());
                if (!string.IsNullOrWhiteSpace(City)) parts.Add(City.Trim());
                if (!string.IsNullOrWhiteSpace(StateOrProvince)) parts.Add(StateOrProvince.Trim());
                if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode.Trim());
                if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country.Trim());
                return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
            }
        }
        public string Status => IsActive ? "Active" : "Archived";
        public string NameDisplay => FullName;
        public string StatusDisplay => Status;
    }

    public class CustomerHistoryDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public int? LoyaltyPoints { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CustomerHistoryRepairDto> Repairs { get; set; } = new();
        public List<CustomerHistoryInteractionDto> Interactions { get; set; } = new();
        public List<CustomerHistoryFollowUpDto> FollowUps { get; set; } = new();

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Address)) parts.Add(Address.Trim());
                if (!string.IsNullOrWhiteSpace(City)) parts.Add(City.Trim());
                if (!string.IsNullOrWhiteSpace(StateOrProvince)) parts.Add(StateOrProvince.Trim());
                if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode.Trim());
                if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country.Trim());
                return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
            }
        }
    }

    public class CustomerHistoryRepairDto
    {
        public int RepairRequestId { get; set; }
        public string RequestNumber { get; set; } = "";
        public string DeviceModel { get; set; } = "";
        public string IssueDescription { get; set; } = "";
        public int Status { get; set; }
        public int Priority { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal? ActualCost { get; set; }

        public string StatusText => Status switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "In Progress",
            3 => "Completed",
            4 => "Rejected",
            5 => "Reassigned",
            _ => "—"
        };
        public string PriorityText => Priority switch
        {
            0 => "Low",
            1 => "Medium",
            2 => "High",
            3 => "Urgent",
            _ => "—"
        };
        public string CostDisplay => ActualCost.HasValue
            ? $"₱{ActualCost.Value:N2}" : "—";
    }

    public class CustomerHistoryInteractionDto
    {
        public int CustomerInteractionId { get; set; }
        public int InteractionType { get; set; }
        public int Status { get; set; }
        public int Priority { get; set; }
        public string Subject { get; set; } = "";
        public string Notes { get; set; } = "";
        public string? Resolution { get; set; }
        public DateTime InteractionDate { get; set; }
        public DateTime? ClosedAt { get; set; }

        public string TypeText => InteractionType switch
        {
            0 => "Inquiry",
            1 => "Complaint",
            2 => "Feedback",
            _ => "—"
        };
        public string StatusText => Status switch
        {
            0 => "Open",
            1 => "In Progress",
            2 => "Closed",
            _ => "—"
        };
    }

    public class CustomerHistoryFollowUpDto
    {
        public int FollowUpId { get; set; }
        public string Subject { get; set; } = "";
        public string Notes { get; set; } = "";
        public int Channel { get; set; }
        public int Status { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string ChannelText => Channel switch
        {
            0 => "Call",
            1 => "Email",
            2 => "SMS",
            3 => "Visit",
            _ => "—"
        };
        public string StatusText => Status switch
        {
            0 => "Scheduled",
            1 => "Completed",
            2 => "Cancelled",
            _ => "—"
        };
    }
}
