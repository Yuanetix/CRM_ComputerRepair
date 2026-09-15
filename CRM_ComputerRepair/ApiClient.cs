using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.winforms
{
    public class ApiClient
    {
        // ⚠️ CHANGE PORT if your API runs on a different one
        private const string BaseUrl = "https://localhost:7042";

        // Hardcoded tenant for the exam demo (Company 1)
        private const int CompanyId = 1;

        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (msg, cert, chain, errors) => true
            };

            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        // ═══════════════════════════════════════════════════════
        // CUSTOMERS
        // ═══════════════════════════════════════════════════════

        public async Task<List<CustomerDto>> GetCustomersAsync(bool includeArchived = false)
        {
            var url = includeArchived
                ? $"/tenant/{CompanyId}/customers?includeArchived=true"
                : $"/tenant/{CompanyId}/customers";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<CustomerDto>>(json, _jsonOptions);
            return result ?? new List<CustomerDto>();
        }

        public async Task<CustomerDto?> GetCustomerAsync(int customerId)
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/customers/{customerId}");
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CustomerDto>(json, _jsonOptions);
        }

        public async Task<CustomerDto?> CreateCustomerAsync(CustomerDto customer)
        {
            var content = ToJsonContent(customer);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/customers", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CustomerDto>(json, _jsonOptions);
        }

        public async Task<CustomerDto?> UpdateCustomerAsync(int customerId, CustomerDto customer)
        {
            var content = ToJsonContent(customer);
            var response = await _http.PutAsync(
                $"/tenant/{CompanyId}/customers/{customerId}", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CustomerDto>(json, _jsonOptions);
        }

        public async Task ArchiveCustomerAsync(int customerId)
        {
            var response = await _http.DeleteAsync(
                $"/tenant/{CompanyId}/customers/{customerId}");
            await EnsureSuccess(response);
        }

        public async Task RestoreCustomerAsync(int customerId)
        {
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/customers/{customerId}/restore", null);
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // INTERACTIONS — Inquiry / Complaint / Feedback
        // ═══════════════════════════════════════════════════════

        public async Task<List<InteractionDto>> GetInteractionsAsync(
            InteractionTypeFilter? type = null,
            bool includeArchived = false)
        {
            var url = $"/tenant/{CompanyId}/interactions";
            var qs = new List<string>();

            if (type.HasValue)
                qs.Add($"type={(int)type.Value}");

            if (includeArchived)
                qs.Add("includeArchived=true");

            if (qs.Count > 0)
                url += "?" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<InteractionDto>>(json, _jsonOptions);
            return result ?? new List<InteractionDto>();
        }

        public async Task<InteractionDto?> GetInteractionAsync(int interactionId)
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/interactions/{interactionId}");
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<InteractionDto>(json, _jsonOptions);
        }

        public async Task<InteractionDto?> CreateInteractionAsync(InteractionDto interaction)
        {
            var content = ToJsonContent(interaction);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/interactions", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<InteractionDto>(json, _jsonOptions);
        }

        public async Task<InteractionDto?> UpdateInteractionAsync(
            int interactionId, InteractionDto interaction)
        {
            var content = ToJsonContent(interaction);
            var response = await _http.PutAsync(
                $"/tenant/{CompanyId}/interactions/{interactionId}", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<InteractionDto>(json, _jsonOptions);
        }

        public async Task ArchiveInteractionAsync(int interactionId)
        {
            var response = await _http.DeleteAsync(
                $"/tenant/{CompanyId}/interactions/{interactionId}");
            await EnsureSuccess(response);
        }

        public async Task RestoreInteractionAsync(int interactionId)
        {
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/interactions/{interactionId}/restore", null);
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // FOLLOW-UPS
        // ═══════════════════════════════════════════════════════

        public async Task<List<FollowUpDto>> GetFollowUpsAsync(
            FollowUpStatusFilter? status = null,
            bool includeArchived = false)
        {
            var url = $"/tenant/{CompanyId}/follow-ups";
            var qs = new List<string>();

            if (status.HasValue)
                qs.Add($"status={(int)status.Value}");

            if (includeArchived)
                qs.Add("includeArchived=true");

            if (qs.Count > 0)
                url += "?" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<FollowUpDto>>(json, _jsonOptions);
            return result ?? new List<FollowUpDto>();
        }

        public async Task<FollowUpDto?> GetFollowUpAsync(int followUpId)
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/follow-ups/{followUpId}");
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FollowUpDto>(json, _jsonOptions);
        }

        public async Task<FollowUpDto?> CreateFollowUpAsync(FollowUpDto followUp)
        {
            var content = ToJsonContent(followUp);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/follow-ups", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FollowUpDto>(json, _jsonOptions);
        }

        public async Task<FollowUpDto?> UpdateFollowUpAsync(
            int followUpId, FollowUpDto followUp)
        {
            var content = ToJsonContent(followUp);
            var response = await _http.PutAsync(
                $"/tenant/{CompanyId}/follow-ups/{followUpId}", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FollowUpDto>(json, _jsonOptions);
        }

        public async Task ArchiveFollowUpAsync(int followUpId)
        {
            var response = await _http.DeleteAsync(
                $"/tenant/{CompanyId}/follow-ups/{followUpId}");
            await EnsureSuccess(response);
        }

        public async Task RestoreFollowUpAsync(int followUpId)
        {
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/follow-ups/{followUpId}/restore", null);
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // REPAIR REQUESTS
        // ═══════════════════════════════════════════════════════

        public async Task<List<RepairRequestDto>> GetRepairRequestsAsync(
            RepairStatusFilter? status = null)
        {
            var url = $"/tenant/{CompanyId}/repair-requests";
            if (status.HasValue)
                url += $"?status={(int)status.Value}";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<RepairRequestDto>>(json, _jsonOptions);
            return result ?? new List<RepairRequestDto>();
        }

        public async Task<RepairRequestDto?> GetRepairRequestAsync(int repairRequestId)
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/repair-requests/{repairRequestId}");
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RepairRequestDto>(json, _jsonOptions);
        }

        public async Task<RepairRequestDto?> CreateRepairRequestAsync(RepairRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/repair-requests", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RepairRequestDto>(json, _jsonOptions);
        }

        public async Task<RepairRequestDto?> UpdateRepairRequestAsync(
            int repairRequestId, RepairRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PutAsync(
                $"/tenant/{CompanyId}/repair-requests/{repairRequestId}", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RepairRequestDto>(json, _jsonOptions);
        }

        // ═══════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════

        private StringContent ToJsonContent(object value)
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static async Task EnsureSuccess(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"API error {(int)response.StatusCode}: {error}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // DTO: Customer
    // ═══════════════════════════════════════════════════════════

    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? LoyaltyPoints { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Status => IsActive ? "Active" : "Archived";
    }

    // ═══════════════════════════════════════════════════════════
    // ENUMS — filter helpers (must match domain enums)
    // ═══════════════════════════════════════════════════════════

    public enum InteractionTypeFilter
    {
        Inquiry = 0,
        Complaint = 1,
        Feedback = 2
    }

    public enum FollowUpStatusFilter
    {
        Scheduled = 0,
        Completed = 1,
        Cancelled = 2
    }

    public enum RepairStatusFilter
    {
        Pending = 0,
        Approved = 1,
        InProgress = 2,
        Completed = 3,
        Rejected = 4,
        Reassigned = 5
    }

    // ═══════════════════════════════════════════════════════════
    // DTO: Interaction (Inquiry / Complaint / Feedback)
    // ═══════════════════════════════════════════════════════════

    public class InteractionDto
    {
        public int CustomerInteractionId { get; set; }
        public int? CustomerId { get; set; }
        public int? RepairRequestId { get; set; }

        // 0 = Inquiry | 1 = Complaint | 2 = Feedback
        public int InteractionType { get; set; }

        // 0 = Open | 1 = InProgress | 2 = Closed
        public int Status { get; set; }

        // 0 = Low | 1 = Medium | 2 = High
        public int Priority { get; set; }

        public string Subject { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string? Resolution { get; set; }
        public string? InteractionByUserId { get; set; }

        public DateTime InteractionDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public bool IsActive { get; set; }

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

        public string PriorityText => Priority switch
        {
            0 => "Low",
            1 => "Medium",
            2 => "High",
            _ => "—"
        };

        public string ActivityStatus => IsActive ? "Active" : "Archived";
    }

    // ═══════════════════════════════════════════════════════════
    // DTO: FollowUp
    // ═══════════════════════════════════════════════════════════

    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int? CustomerId { get; set; }
        public int? RepairRequestId { get; set; }

        public string Subject { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public DateTime ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // 0 = Call | 1 = Email | 2 = SMS | 3 = Visit
        public int Channel { get; set; }

        // 0 = Scheduled | 1 = Completed | 2 = Cancelled
        public int Status { get; set; }

        public string? AssignedToUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }

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

        public string ActivityStatus => IsActive ? "Active" : "Archived";
    }

    // ═══════════════════════════════════════════════════════════
    // DTO: RepairRequest
    // ═══════════════════════════════════════════════════════════

    public class RepairRequestDto
    {
        public int RepairRequestId { get; set; }
        public string RequestNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }
        public int? DeviceId { get; set; }

        public string DeviceModel { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string IssueDescription { get; set; } = string.Empty;

        // 0 = Pending | 1 = Approved | 2 = InProgress
        // 3 = Completed | 4 = Rejected | 5 = Reassigned
        public int Status { get; set; }

        // 0 = Low | 1 = Medium | 2 = High | 3 = Urgent
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
    }
}