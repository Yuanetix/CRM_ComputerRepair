using System;

namespace CRM.winforms
{
    public enum InteractionTypeFilter { Inquiry = 0, Complaint = 1, Feedback = 2 }
    public enum FollowUpStatusFilter { Scheduled = 0, Completed = 1, Cancelled = 2 }

    public class InteractionDto
    {
        public int CustomerInteractionId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public int? RepairRequestId { get; set; }
        public string? RepairRequestNumber { get; set; }
        public string? DeviceModel { get; set; }
        public int InteractionType { get; set; }
        public int Status { get; set; }
        public int Priority { get; set; }
        public string Subject { get; set; } = "";
        public string Notes { get; set; } = "";
        public string? Resolution { get; set; }
        public DateTime InteractionDate { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? HandledByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }

        public string TypeText => InteractionType switch { 0 => "Inquiry", 1 => "Complaint", 2 => "Feedback", _ => "—" };
        public string StatusText => Status switch { 0 => "Open", 1 => "In Progress", 2 => "Closed", _ => "—" };
        public string PriorityText => Priority switch { 0 => "Low", 1 => "Medium", 2 => "High", _ => "—" };
        public string ActivityStatus => IsActive ? "Active" : "Archived";
        public string ClosedAtDisplay => ClosedAt.HasValue ? ClosedAt.Value.ToString("MMM d  HH:mm") : "—";

        public string CustomerDisplay => !string.IsNullOrWhiteSpace(CustomerName)
            ? CustomerName
            : (CustomerId.HasValue ? $"Customer #{CustomerId}" : "Walk-in");

        public string ContactDisplay => !string.IsNullOrWhiteSpace(CustomerPhone)
            ? CustomerPhone
            : (!string.IsNullOrWhiteSpace(CustomerEmail) ? CustomerEmail : "No contact");

        public string RepairDisplay => !string.IsNullOrWhiteSpace(RepairRequestNumber)
            ? $"{RepairRequestNumber}{(string.IsNullOrWhiteSpace(DeviceModel) ? "" : " · " + DeviceModel)}"
            : (RepairRequestId.HasValue ? $"Repair #{RepairRequestId}" : "General Support");
    }

    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public int? RepairRequestId { get; set; }
        public string? RepairRequestNumber { get; set; }
        public string? DeviceModel { get; set; }
        public string Subject { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int Channel { get; set; }
        public int Status { get; set; }
        public string? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }

        public string ChannelText => Channel switch { 0 => "Call", 1 => "Email", 2 => "SMS", 3 => "Visit", _ => "—" };
        public string StatusText => Status switch { 0 => "Scheduled", 1 => "Completed", 2 => "Cancelled", _ => "—" };
        public string ActivityStatus => IsActive ? "Active" : "Archived";
        public string CompletedAtDisplay => CompletedAt.HasValue
            ? CompletedAt.Value.ToString("MMM d  HH:mm") : "—";

        public string CustomerDisplay => !string.IsNullOrWhiteSpace(CustomerName)
            ? CustomerName
            : (CustomerId.HasValue ? $"Customer #{CustomerId}" : "Walk-in");

        public string ContactDisplay => !string.IsNullOrWhiteSpace(CustomerPhone)
            ? CustomerPhone
            : (!string.IsNullOrWhiteSpace(CustomerEmail) ? CustomerEmail : "No contact");

        public string RepairDisplay => !string.IsNullOrWhiteSpace(RepairRequestNumber)
            ? $"{RepairRequestNumber}{(string.IsNullOrWhiteSpace(DeviceModel) ? "" : " · " + DeviceModel)}"
            : (RepairRequestId.HasValue ? $"Repair #{RepairRequestId}" : "General Follow-Up");

        public bool IsOverdue => Status == 0 && ScheduledAt.Date < DateTime.Today;
        public bool IsDueToday => Status == 0 && ScheduledAt.Date == DateTime.Today;

        public string UrgencyText => Status switch
        {
            1 => "Completed",
            2 => "Cancelled",
            _ when IsOverdue => "Overdue",
            _ when IsDueToday => "Due Today",
            _ => "Upcoming"
        };
    }
}
