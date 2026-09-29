using System;

namespace CRM.winforms
{
    public enum RepairStatusFilter
    {
        Pending = 0,
        Approved = 1,
        InProgress = 2,
        Completed = 3,
        Rejected = 4,
        Reassigned = 5
    }

    public class RepairRequestDto
    {
        public int RepairRequestId { get; set; }
        public string RequestNumber { get; set; } = "";
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public int? DeviceId { get; set; }
        public string DeviceModel { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string IssueDescription { get; set; } = "";
        public int Status { get; set; }
        public int Priority { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public decimal? PartsCost { get; set; }
        public decimal? LaborCost { get; set; }
        public string? TechnicianNotes { get; set; }
        public string? AssignedToStaffId { get; set; }
        public string? AssignedToManagerId { get; set; }

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

        public string CustomerDisplay => !string.IsNullOrWhiteSpace(CustomerName)
            ? CustomerName
            : $"Customer #{CustomerId}";

        public string ContactDisplay => !string.IsNullOrWhiteSpace(CustomerPhone)
            ? CustomerPhone
            : (!string.IsNullOrWhiteSpace(CustomerEmail) ? CustomerEmail : "No contact");

        public string DeviceDisplay => !string.IsNullOrWhiteSpace(DeviceModel)
            ? DeviceModel
            : "Generic Device";

        public string SerialDisplay => !string.IsNullOrWhiteSpace(SerialNumber)
            ? SerialNumber
            : "—";

        public string CostDisplay => ActualCost.HasValue && ActualCost > 0
            ? $"₱{ActualCost.Value:N2}"
            : (EstimatedCost.HasValue ? $"Est: ₱{EstimatedCost.Value:N2}" : "—");

        public string CompletionDisplay => CompletionDate.HasValue
            ? CompletionDate.Value.ToString("MMM d  HH:mm")
            : (Status == 3 ? "Completed" : "In Queue");
    }

    public class StaffActivityDto
    {
        public int RepairStatusHistoryId { get; set; }
        public int RepairRequestId { get; set; }
        public string RequestNumber { get; set; } = "";
        public string DeviceModel { get; set; } = "";
        public int OldStatus { get; set; }
        public int NewStatus { get; set; }
        public string? ChangedByUserId { get; set; }
        public string? Notes { get; set; }
        public DateTime ChangedAt { get; set; }

        public string OldStatusText => OldStatus switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "In Progress",
            3 => "Completed",
            4 => "Rejected",
            5 => "Reassigned",
            _ => "—"
        };
        public string NewStatusText => NewStatus switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "In Progress",
            3 => "Completed",
            4 => "Rejected",
            5 => "Reassigned",
            _ => "—"
        };
        public string ChangeDisplay => $"{OldStatusText} → {NewStatusText}";
        public string StaffDisplay => string.IsNullOrWhiteSpace(ChangedByUserId)
            ? "(system)" : ChangedByUserId;
        public string WhenDisplay => ChangedAt.ToString("MMM d, yyyy HH:mm");
    }
}
