using System;
using System.Collections.Generic;

namespace CRM.winforms
{
    public class CompanyDto
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; } = "Philippines";
        public bool IsActive { get; set; } = true;
        public bool HasAcceptedTerms { get; set; } = false;
        public DateTime? TermsAcceptedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Multi-tenant database details
        public string DatabaseServer { get; set; } = @"(localdb)\MSSQLLocalDB";
        public string DatabaseName { get; set; } = "";
        public string? CredentialKey { get; set; }

        // Subscription details
        public int? SubscriptionId { get; set; }
        public string? SubscriptionPlanName { get; set; }
        public decimal? SubscriptionPrice { get; set; }
        public string? SubscriptionDuration { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxDevices { get; set; }
        public bool EnableMultiBranching { get; set; }

        // Initial / Assigned Business Admin
        public string? AdminUserId { get; set; }
        public string? AdminFullName { get; set; }
        public string? AdminEmail { get; set; }
        public string? AdminUsername { get; set; }

        // Counts
        public int TotalUsersCount { get; set; }
        public int TotalDevicesCount { get; set; }

        // Display helpers
        public string StatusText => IsActive ? "Active" : "Deactivated";
        public string TermsStatusDisplay => HasAcceptedTerms
            ? (TermsAcceptedAt.HasValue ? $"Accepted ({TermsAcceptedAt.Value:MMM dd, yyyy})" : "Accepted")
            : "Pending Acceptance";

        public string LocationDisplay
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(City)) parts.Add(City);
                if (!string.IsNullOrWhiteSpace(StateOrProvince)) parts.Add(StateOrProvince);
                if (parts.Count == 0 && !string.IsNullOrWhiteSpace(Country)) parts.Add(Country);
                return parts.Count > 0 ? string.Join(", ", parts) : "—";
            }
        }

        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Address)) parts.Add(Address);
                if (!string.IsNullOrWhiteSpace(City)) parts.Add(City);
                if (!string.IsNullOrWhiteSpace(StateOrProvince)) parts.Add(StateOrProvince);
                if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode);
                if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);
                return parts.Count > 0 ? string.Join(", ", parts) : "—";
            }
        }

        public string PlanDisplay => !string.IsNullOrWhiteSpace(SubscriptionPlanName)
            ? $"{SubscriptionPlanName} (₱{SubscriptionPrice:N0})"
            : "No Plan";
    }

    public class RegisterCompanyRequestDto
    {
        public string CompanyName { get; set; } = "";
        public string CompanyCode { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; } = "Philippines";
        public string? DatabaseServer { get; set; } = @"(localdb)\MSSQLLocalDB";
        public string? DatabaseName { get; set; }
        public string? CredentialKey { get; set; }
        public int? SubscriptionId { get; set; }
        public string AdminFirstName { get; set; } = "";
        public string AdminLastName { get; set; } = "";
        public string AdminEmail { get; set; } = "";
        public string AdminUsername { get; set; } = "";
        public string AdminPassword { get; set; } = "";
    }

    public class UpdateCompanyRequestDto
    {
        public string CompanyName { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; } = "Philippines";
        public int? SubscriptionId { get; set; }
        public string? DatabaseServer { get; set; }
        public string? DatabaseName { get; set; }
        public bool IsActive { get; set; }
    }

    public class SubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int? CompanyId { get; set; }
        public string SubscriptionName { get; set; } = "";
        public decimal PricePerMonth { get; set; }
        public int DurationMonths { get; set; } = 1;
        public string Duration { get; set; } = "1 Month";
        public int MaxUsers { get; set; } = 5;
        public int MaxDevices { get; set; } = 100;
        public bool EnableMultiBranching { get; set; } = false;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; } = false;
        public string? BillingCycle { get; set; } = "Monthly";
        public int SubscribedCompaniesCount { get; set; }

        public string StatusText => IsArchived ? "Archived" : (IsActive ? "Active" : "Inactive");
        public string PriceDisplay => $"₱{PricePerMonth:N2}";
        public string DurationDisplay => !string.IsNullOrWhiteSpace(Duration) ? Duration : $"{DurationMonths} Month(s)";
        public string MultiBranchDisplay => EnableMultiBranching ? "Enabled" : "Single Branch";
        public string StartDateDisplay => StartDate.ToString("MMM d, yyyy");
        public string EndDateDisplay => EndDate.ToString("MMM d, yyyy");
    }

    public class TermsDto
    {
        public int TermsId { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Version { get; set; }
        public bool IsActive { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StatusText => IsActive ? "Active" : "Archived";
        public string VersionDisplay => Version.ToString("MMM d, yyyy HH:mm");
    }

    public class UserSummaryDto
    {
        public string Id { get; set; } = "";
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public int? CompanyId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string> Roles { get; set; } = new();

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string RoleDisplay => Roles.Count > 0 ? string.Join(", ", Roles) : "—";
        public string StatusText => IsActive ? "Active" : "Inactive";
    }

    public class AuditLogDto
    {
        public int AuditLogId { get; set; }
        public string? UserId { get; set; }
        public string Action { get; set; } = "";
        public string Entity { get; set; } = "";
        public string? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }

        public string UserDisplay => string.IsNullOrWhiteSpace(UserId) ? "(system)" : UserId;
        public string WhenDisplay => Timestamp.ToString("MMM d, yyyy HH:mm:ss");
        public string EntityDisplay => string.IsNullOrWhiteSpace(EntityId)
            ? Entity : $"{Entity} #{EntityId}";
    }
}
