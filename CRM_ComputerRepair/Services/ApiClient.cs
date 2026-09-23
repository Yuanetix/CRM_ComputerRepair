using CRM.winforms.Auth;
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
        private const string BaseUrl = "https://localhost:7042";
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

            // Forward the acting user to the API so audit rows are attributed.
            if (!string.IsNullOrWhiteSpace(UserSession.Username))
                _http.DefaultRequestHeaders.Add("X-User-Id", UserSession.Username);

            // Authenticate with the signed JWT issued at login (server-side RBAC).
            if (!string.IsNullOrWhiteSpace(UserSession.Token))
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", UserSession.Token);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        // ═══════════════════════════════════════════════════════
        // AUTH
        // ═══════════════════════════════════════════════════════

        public async Task<LoginResponseDto?> LoginAsync(string username, string password)
        {
            var body = new { username, password };
            var content = ToJsonContent(body);

            var response = await _http.PostAsync("/auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                string message = "Invalid username or password.";
                try
                {
                    using var doc = JsonDocument.Parse(errorJson);
                    if (doc.RootElement.TryGetProperty("error", out var errProp))
                        message = errProp.GetString() ?? message;
                }
                catch { }

                throw new Exception(message);
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LoginResponseDto>(json, _jsonOptions);
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
        // INTERACTIONS
        // ═══════════════════════════════════════════════════════

        public async Task<List<InteractionDto>> GetInteractionsAsync(
            InteractionTypeFilter? type = null, bool includeArchived = false)
        {
            var url = $"/tenant/{CompanyId}/interactions";
            var qs = new List<string>();
            if (type.HasValue) qs.Add($"type={(int)type.Value}");
            if (includeArchived) qs.Add("includeArchived=true");
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

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
            FollowUpStatusFilter? status = null, bool includeArchived = false)
        {
            var url = $"/tenant/{CompanyId}/follow-ups";
            var qs = new List<string>();
            if (status.HasValue) qs.Add($"status={(int)status.Value}");
            if (includeArchived) qs.Add("includeArchived=true");
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

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
            if (status.HasValue) url += $"?status={(int)status.Value}";

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
        // CUSTOMER HISTORY
        // ═══════════════════════════════════════════════════════

        public async Task<CustomerHistoryDto?> GetCustomerHistoryAsync(int customerId)
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/customer-history/{customerId}");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CustomerHistoryDto>(json, _jsonOptions);
        }

        // ═══════════════════════════════════════════════════════
        // STAFF ACTIVITY
        // ═══════════════════════════════════════════════════════

        public async Task<List<StaffActivityDto>> GetStaffActivityAsync(
            string? staffId = null, int take = 200)
        {
            var url = $"/tenant/{CompanyId}/staff-activity?take={take}";
            if (!string.IsNullOrWhiteSpace(staffId))
                url += $"&staffId={Uri.EscapeDataString(staffId)}";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<StaffActivityDto>>(json, _jsonOptions);
            return result ?? new List<StaffActivityDto>();
        }

        // ═══════════════════════════════════════════════════════
        // LOYALTY PROGRAMS
        // ═══════════════════════════════════════════════════════

        public async Task<List<LoyaltyProgramDto>> GetLoyaltyProgramsAsync(bool activeOnly = false)
        {
            var url = "/loyalty-programs";
            if (activeOnly) url += "?activeOnly=true";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<LoyaltyProgramDto>>(json, _jsonOptions);
            return result ?? new List<LoyaltyProgramDto>();
        }

        public async Task<LoyaltyProgramDto?> CreateLoyaltyProgramAsync(LoyaltyProgramDto dto)
        {
            var body = new
            {
                programName = dto.ProgramName,
                description = dto.Description,
                pointsPerPeso = dto.PointsPerPeso,
                discountPercentage = dto.DiscountPercentage,
                minimumSpend = dto.MinimumSpend,
                startDate = dto.StartDate,
                endDate = dto.EndDate,
                pointsValidityDays = dto.PointsValidityDays,
                redeemPointsRequired = dto.RedeemPointsRequired,
                minTransactions = dto.MinTransactions,
                minTotalSpent = dto.MinTotalSpent,
                maxInactiveDays = dto.MaxInactiveDays,
                minVisitsPerPeriod = dto.MinVisitsPerPeriod,
                visitPeriodDays = dto.VisitPeriodDays,
                rewardType = dto.RewardType,
                rewardValue = dto.RewardValue,
                maxRedemptionsPerCustomer = dto.MaxRedemptionsPerCustomer
            };

            var content = ToJsonContent(body);
            var response = await _http.PostAsync("/loyalty-programs", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LoyaltyProgramDto>(json, _jsonOptions);
        }

        public async Task<LoyaltyProgramDto?> UpdateLoyaltyProgramAsync(int id, LoyaltyProgramDto dto)
        {
            var body = new
            {
                programName = dto.ProgramName,
                description = dto.Description,
                pointsPerPeso = dto.PointsPerPeso,
                discountPercentage = dto.DiscountPercentage,
                minimumSpend = dto.MinimumSpend,
                startDate = dto.StartDate,
                endDate = dto.EndDate,
                pointsValidityDays = dto.PointsValidityDays,
                redeemPointsRequired = dto.RedeemPointsRequired,
                minTransactions = dto.MinTransactions,
                minTotalSpent = dto.MinTotalSpent,
                maxInactiveDays = dto.MaxInactiveDays,
                minVisitsPerPeriod = dto.MinVisitsPerPeriod,
                visitPeriodDays = dto.VisitPeriodDays,
                rewardType = dto.RewardType,
                rewardValue = dto.RewardValue,
                maxRedemptionsPerCustomer = dto.MaxRedemptionsPerCustomer,
                isActive = dto.IsActive
            };

            var content = ToJsonContent(body);
            var response = await _http.PutAsync($"/loyalty-programs/{id}", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LoyaltyProgramDto>(json, _jsonOptions);
        }

        public async Task ArchiveLoyaltyProgramAsync(int id)
        {
            var response = await _http.DeleteAsync($"/loyalty-programs/{id}");
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // SUBSCRIPTIONS
        // ═══════════════════════════════════════════════════════

        public async Task<List<SubscriptionDto>> GetSubscriptionsAsync(bool activeOnly = false)
        {
            var url = "/subscriptions";
            if (activeOnly) url += "?activeOnly=true";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<SubscriptionDto>>(json, _jsonOptions);
            return result ?? new List<SubscriptionDto>();
        }

        public async Task<SubscriptionDto?> CreateSubscriptionAsync(SubscriptionDto dto)
        {
            var body = new
            {
                subscriptionName = dto.SubscriptionName,
                pricePerMonth = dto.PricePerMonth,
                maxUsers = dto.MaxUsers,
                maxDevices = dto.MaxDevices,
                startDate = dto.StartDate,
                endDate = dto.EndDate,
                billingCycle = dto.BillingCycle
            };

            var content = ToJsonContent(body);
            var response = await _http.PostAsync("/subscriptions", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SubscriptionDto>(json, _jsonOptions);
        }

        public async Task<SubscriptionDto?> UpdateSubscriptionAsync(int id, SubscriptionDto dto)
        {
            var body = new
            {
                subscriptionName = dto.SubscriptionName,
                pricePerMonth = dto.PricePerMonth,
                maxUsers = dto.MaxUsers,
                maxDevices = dto.MaxDevices,
                startDate = dto.StartDate,
                endDate = dto.EndDate,
                isActive = dto.IsActive,
                billingCycle = dto.BillingCycle
            };

            var content = ToJsonContent(body);
            var response = await _http.PutAsync($"/subscriptions/{id}", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SubscriptionDto>(json, _jsonOptions);
        }

        public async Task ArchiveSubscriptionAsync(int id)
        {
            var response = await _http.DeleteAsync($"/subscriptions/{id}");
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // TERMS & CONDITIONS
        // ═══════════════════════════════════════════════════════

        public async Task<List<TermsDto>> GetTermsAsync()
        {
            var response = await _http.GetAsync("/terms");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<TermsDto>>(json, _jsonOptions);
            return result ?? new List<TermsDto>();
        }

        public async Task<TermsDto?> GetActiveTermsAsync()
        {
            var response = await _http.GetAsync("/terms/active");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TermsDto>(json, _jsonOptions);
        }

        public async Task<TermsDto?> CreateTermsAsync(TermsDto dto)
        {
            var body = new
            {
                title = dto.Title,
                content = dto.Content,
                createdByUserId = UserSession.UserId
            };

            var content = ToJsonContent(body);
            var response = await _http.PostAsync("/terms", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TermsDto>(json, _jsonOptions);
        }

        public async Task<TermsDto?> UpdateTermsAsync(int id, TermsDto dto)
        {
            var body = new
            {
                title = dto.Title,
                content = dto.Content,
                isActive = dto.IsActive
            };

            var content = ToJsonContent(body);
            var response = await _http.PutAsync($"/terms/{id}", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TermsDto>(json, _jsonOptions);
        }

        public async Task ActivateTermsAsync(int id)
        {
            var response = await _http.PostAsync($"/terms/{id}/activate", null);
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // USERS
        // ═══════════════════════════════════════════════════════

        public async Task<List<UserSummaryDto>> GetUsersAsync(bool activeOnly = false)
        {
            var url = "/users";
            if (activeOnly) url += "?activeOnly=true";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<UserSummaryDto>>(json, _jsonOptions);
            return result ?? new List<UserSummaryDto>();
        }

        public async Task<UserSummaryDto?> CreateUserAsync(
            string username, string email, string password,
            string firstName, string lastName, string role)
        {
            var body = new { userName = username, email, password, firstName, lastName, role };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync("/users", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserSummaryDto>(json, _jsonOptions);
        }

        public async Task<UserSummaryDto?> UpdateUserAsync(
            string id, string firstName, string lastName,
            string? email, string? userName, bool isActive, string? role)
        {
            var body = new
            {
                firstName,
                lastName,
                email,
                userName,
                isActive,
                role
            };
            var content = ToJsonContent(body);
            var response = await _http.PutAsync($"/users/{id}", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserSummaryDto>(json, _jsonOptions);
        }

        public async Task DeactivateUserAsync(string id)
        {
            var response = await _http.DeleteAsync($"/users/{id}");
            await EnsureSuccess(response);
        }

        public async Task RestoreUserAsync(string id)
        {
            var response = await _http.PostAsync($"/users/{id}/restore", null);
            await EnsureSuccess(response);
        }

        // ═══════════════════════════════════════════════════════
        // ADMIN ACCOUNTS
        // ═══════════════════════════════════════════════════════

        public async Task<List<UserSummaryDto>> GetAdminAccountsAsync()
        {
            var response = await _http.GetAsync("/admin-accounts");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<UserSummaryDto>>(json, _jsonOptions);
            return result ?? new List<UserSummaryDto>();
        }

        // ═══════════════════════════════════════════════════════
        // AUDIT
        // ═══════════════════════════════════════════════════════

        public async Task<List<AuditLogDto>> GetAuditLogAsync(
            string? userId = null, string? entity = null, int take = 200)
        {
            var qs = new List<string> { $"take={take}" };
            if (!string.IsNullOrWhiteSpace(userId))
                qs.Add($"userId={Uri.EscapeDataString(userId)}");
            if (!string.IsNullOrWhiteSpace(entity))
                qs.Add($"entity={Uri.EscapeDataString(entity)}");

            var url = "/audit?" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<AuditLogDto>>(json, _jsonOptions);
            return result ?? new List<AuditLogDto>();
        }

        // ═══════════════════════════════════════════════════════
        // ANALYTICS
        // ═══════════════════════════════════════════════════════

        public async Task<DashboardDto?> GetDashboardAsync()
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/analytics/dashboard");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DashboardDto>(json, _jsonOptions);
        }

        /// <summary>Generic drill-down: customers, transactions, sales, services, interactions, loyalty.</summary>
        public async Task<AnalyticsDetailsDto?> GetAnalyticsDetailsAsync(
            string metric, DateTime? from = null, DateTime? to = null,
            int? customerId = null, string? service = null, string? status = null)
        {
            var qs = new List<string>();
            const string iso = "o";
            if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString(iso))}");
            if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString(iso))}");
            if (customerId.HasValue) qs.Add($"customerId={customerId.Value}");
            if (!string.IsNullOrWhiteSpace(service)) qs.Add($"service={Uri.EscapeDataString(service)}");
            if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");

            var url = $"/tenant/{CompanyId}/analytics/details?metric={Uri.EscapeDataString(metric)}";
            if (qs.Count > 0) url += "&" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AnalyticsDetailsDto>(json, _jsonOptions);
        }

        /// <summary>Active customers with no completed transaction in the last 90 days.</summary>
        public async Task<AnalyticsDetailsDto?> GetInactiveCustomersAsync()
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/analytics/inactive-customers");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AnalyticsDetailsDto>(json, _jsonOptions);
        }

        /// <summary>Per-customer visit frequency from completed transactions.</summary>
        public async Task<List<CustomerVisitDto>> GetCustomerVisitsAsync()
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/analytics/visits");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<CustomerVisitDto>>(json, _jsonOptions);
            return result ?? new List<CustomerVisitDto>();
        }

        /// <summary>Loyalty members joined with customer names and real spend.</summary>
        public async Task<List<LoyaltyMemberDetailDto>> GetLoyaltyMembersAsync(int? programId = null)
        {
            var url = $"/tenant/{CompanyId}/analytics/loyalty-members";
            if (programId.HasValue) url += $"?programId={programId.Value}";

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<LoyaltyMemberDetailDto>>(json, _jsonOptions);
            return result ?? new List<LoyaltyMemberDetailDto>();
        }

        /// <summary>
        /// Retention recommendations with a visible, human-readable basis
        /// (transaction count, last visit, spending, visit frequency, inactivity).
        /// </summary>
        public async Task<List<RetentionRecommendationDto>> GetRetentionRecommendationsAsync(
            string? category = null, string? search = null, decimal? minSpend = null)
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(category)) qs.Add($"category={Uri.EscapeDataString(category)}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            if (minSpend.HasValue && minSpend.Value > 0) qs.Add($"minSpend={minSpend.Value}");

            var url = $"/tenant/{CompanyId}/retention/recommendations";
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<RetentionRecommendationDto>>(json, _jsonOptions);
            return result ?? new List<RetentionRecommendationDto>();
        }

        public async Task<List<RetentionCandidateDto>> GetRetentionCandidatesAsync()
        {
            var response = await _http.GetAsync(
                $"/tenant/{CompanyId}/analytics/retention-candidates");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<RetentionCandidateDto>>(json, _jsonOptions);
            return result ?? new List<RetentionCandidateDto>();
        }

        public async Task<bool> LogRetentionContactAsync(
            int customerId, string subject, string notes,
            int? followUpInDays = null, string? category = null, string? basis = null)
        {
            var body = new
            {
                subject,
                notes,
                performedByUserId = UserSession.UserId,
                scheduleFollowUpInDays = followUpInDays,
                category,
                basis
            };

            var content = ToJsonContent(body);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/retention/{customerId}/contact", content);

            await EnsureSuccess(response);
            return true;
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
            throw new Exception($"API error {(int)response.StatusCode}: {error}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // DTOs
    // ═══════════════════════════════════════════════════════════

    public class LoginResponseDto
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Email { get; set; } = "";
        public int CompanyId { get; set; }
        public string Token { get; set; } = "";
    }

    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? LoyaltyPoints { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Status => IsActive ? "Active" : "Archived";
        public string NameDisplay => FullName;
        public string StatusDisplay => Status;
    }

    public enum InteractionTypeFilter { Inquiry = 0, Complaint = 1, Feedback = 2 }
    public enum FollowUpStatusFilter { Scheduled = 0, Completed = 1, Cancelled = 2 }
    public enum RepairStatusFilter { Pending = 0, Approved = 1, InProgress = 2, Completed = 3, Rejected = 4, Reassigned = 5 }

    public class InteractionDto
    {
        public int CustomerInteractionId { get; set; }
        public int? CustomerId { get; set; }
        public int? RepairRequestId { get; set; }
        public int InteractionType { get; set; }
        public int Status { get; set; }
        public int Priority { get; set; }
        public string Subject { get; set; } = "";
        public string Notes { get; set; } = "";
        public string? Resolution { get; set; }
        public string? InteractionByUserId { get; set; }
        public DateTime InteractionDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public bool IsActive { get; set; }

        public string TypeText => InteractionType switch { 0 => "Inquiry", 1 => "Complaint", 2 => "Feedback", _ => "—" };
        public string StatusText => Status switch { 0 => "Open", 1 => "In Progress", 2 => "Closed", _ => "—" };
        public string PriorityText => Priority switch { 0 => "Low", 1 => "Medium", 2 => "High", _ => "—" };
        public string ActivityStatus => IsActive ? "Active" : "Archived";
    }

    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int? CustomerId { get; set; }
        public int? RepairRequestId { get; set; }
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
    }

    public class RepairRequestDto
    {
        public int RepairRequestId { get; set; }
        public string RequestNumber { get; set; } = "";
        public int CustomerId { get; set; }
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
    }

    public class DashboardDto
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int InactiveCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public int NewThisMonth { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalSales { get; set; }
        public decimal AverageTransactionValue { get; set; }
        public int TransactionsThisMonth { get; set; }
        public int LoyaltyMembers { get; set; }
        public double LoyaltyParticipationRate { get; set; }
        public double AverageVisitsPerCustomer { get; set; }
        public int Inactive90Days { get; set; }
        public double RetentionRate { get; set; }
        public int ChurnRisk { get; set; }
        public int OpenInteractions { get; set; }
        public int RepairsCompletedThisMonth { get; set; }
        public double AverageTurnaroundDays { get; set; }
        public double RepeatCustomerRate { get; set; }
        public TopServiceDto TopService { get; set; } = new();
        public InteractionsByTypeDto InteractionsByType { get; set; } = new();
        public RepairsByStatusDto RepairsByStatus { get; set; } = new();
        public List<TimeSeriesPointDto> CustomersOverTime { get; set; } = new();
        public List<TimeSeriesPointDto> TransactionsOverTime { get; set; } = new();
        public List<SalesPointDto> SalesOverTime { get; set; } = new();
        public List<PopularServiceDto> PopularServices { get; set; } = new();
        public List<ActivityPointDto> CustomerActivityTrend { get; set; } = new();
        public List<RetentionTrendPointDto> RetentionTrend { get; set; } = new();
        public List<LoyaltyPerformanceDto> LoyaltyPerformance { get; set; } = new();
    }

    public class TopServiceDto
    {
        public string Name { get; set; } = "\u2014";
        public int Count { get; set; }
        public decimal Revenue { get; set; }
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

    public class LoyaltyPerformanceDto
    {
        public int LoyaltyProgramId { get; set; }
        public string ProgramName { get; set; } = "";
        public int Members { get; set; }
        public int TotalPoints { get; set; }
        public decimal TotalSpent { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>Generic drill-down response: {metric, title, columns, rows}.</summary>
    public class AnalyticsDetailsDto
    {
        public string Metric { get; set; } = "";
        public string? Title { get; set; }
        public List<DetailColumnDto> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
    }

    public class DetailColumnDto
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "text"; // text | number | currency | date
    }

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

    /// <summary>Retention recommendation with the measurable basis behind it.</summary>
    public class RetentionRecommendationDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public string Basis { get; set; } = "";
        public int? LoyaltyProgramId { get; set; }
        public string? ProgramName { get; set; }
        public string? Reward { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalSpent { get; set; }
        public int DaysSinceLastTransaction { get; set; }
        public int Points { get; set; }

        public string CategoryDisplay => Category switch
        {
            "Discount" => "Discount",
            "Reward" => "Loyalty reward",
            "Follow-up" => "Follow-up",
            "Re-engagement" => "Re-engagement",
            _ => Category
        };
        public string LastVisitDisplay => $"{DaysSinceLastTransaction} days ago";
        public string SpentDisplay => $"\u20b1{TotalSpent:N2}";
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

    public class RetentionTrendPointDto
    {
        public string Month { get; set; } = "";
        public string Label { get; set; } = "";
        public int Active { get; set; }
    }

    public class RetentionCandidateDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int? LoyaltyPoints { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string DisplayName => FullName;
        public int InactiveDays => (int)(DateTime.UtcNow - CreatedAt).TotalDays;
        public string LastContactDisplay => CreatedAt.ToString("MMM d, yyyy");
    }

    public class CustomerHistoryDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? LoyaltyPoints { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CustomerHistoryRepairDto> Repairs { get; set; } = new();
        public List<CustomerHistoryInteractionDto> Interactions { get; set; } = new();
        public List<CustomerHistoryFollowUpDto> FollowUps { get; set; } = new();

        public string FullName => $"{FirstName} {LastName}".Trim();
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

    public class SubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int? CompanyId { get; set; }
        public string SubscriptionName { get; set; } = "";
        public decimal PricePerMonth { get; set; }
        public int MaxUsers { get; set; }
        public int MaxDevices { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public string? BillingCycle { get; set; }

        public string StatusText => IsActive ? "Active" : "Archived";
        public string PriceDisplay => $"₱{PricePerMonth:N2}";
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

    // ═══════════════════════════════════════════════════════════
    // USER / AUDIT DTOs
    // ═══════════════════════════════════════════════════════════

    public class UserSummaryDto
    {
        public string Id { get; set; } = "";
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
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