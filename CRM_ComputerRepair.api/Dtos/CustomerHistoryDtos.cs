namespace CRM_ComputerRepair.api.Dtos;

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
    public string? Country { get; set; } = "Philippines";
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

    public List<CustomerHistoryRepairDto> Repairs { get; set; } = new();
    public List<CustomerHistoryInteractionDto> Interactions { get; set; } = new();
    public List<CustomerHistoryFollowUpDto> FollowUps { get; set; } = new();
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
}