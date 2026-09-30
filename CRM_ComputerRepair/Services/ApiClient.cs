using CRM.winforms.Auth;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.winforms
{
    public class ApiClient
    {
        private const string BaseUrl = "https://localhost:7042";
        private static int CompanyId => UserSession.CompanyId > 0 ? UserSession.CompanyId : 1;

        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (msg, cert, chain, errors) => true
            };

            _http = new HttpClient(new SessionAuthHandler(handler))
            {
                BaseAddress = new Uri(BaseUrl)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Attaches the *current* session's JWT and user id to every request.
        /// ApiClient instances are cached per control/form, so credentials must
        /// be resolved per request — not captured in the constructor — otherwise
        /// a stale (or empty) token from a previous session is reused after
        /// logout and a new sign-in.
        /// </summary>
        private sealed class SessionAuthHandler : DelegatingHandler
        {
            public SessionAuthHandler(HttpMessageHandler inner) : base(inner) { }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (!string.IsNullOrWhiteSpace(UserSession.Token))
                {
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer", UserSession.Token);
                }

                if (!string.IsNullOrWhiteSpace(UserSession.Username))
                {
                    request.Headers.Remove("X-User-Id");
                    request.Headers.Add("X-User-Id", UserSession.Username);
                }

                if (UserSession.CompanyId > 0)
                {
                    request.Headers.Remove("X-Company-Id");
                    request.Headers.Add("X-Company-Id", UserSession.CompanyId.ToString());
                }

                return base.SendAsync(request, cancellationToken);
            }
        }

        // ═══════════════════════════════════════════════════════
        // AUTH
        // ═══════════════════════════════════════════════════════

        public async Task<LoginResponseDto?> LoginAsync(string username, string password, int companyId = 1)
        {
            var body = new { companyId, username, password };
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
        // COMPANIES / TENANTS
        // ═══════════════════════════════════════════════════════

        public async Task<List<CompanyDto>> GetCompaniesAsync()
        {
            var response = await _http.GetAsync("/companies");
            if (!response.IsSuccessStatusCode)
                return new List<CompanyDto>();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<CompanyDto>>(json, _jsonOptions);
            return result ?? new List<CompanyDto>();
        }

        public async Task<CompanyDto?> GetCompanyByIdAsync(int id)
        {
            var response = await _http.GetAsync($"/companies/{id}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CompanyDto>(json, _jsonOptions);
        }

        public async Task<List<CompanyDto>> GetActiveCompaniesAsync()
        {
            var response = await _http.GetAsync("/companies/active");
            if (!response.IsSuccessStatusCode)
                return new List<CompanyDto>();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<CompanyDto>>(json, _jsonOptions);
            return result ?? new List<CompanyDto>();
        }

        public async Task<string> GenerateCompanyCodeAsync()
        {
            try
            {
                var response = await _http.GetAsync("/companies/generate-code");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("companyCode", out var prop))
                        return prop.GetString() ?? $"CMP-{DateTime.UtcNow.Year}-1001";
                }
            }
            catch { }

            return $"CMP-{DateTime.UtcNow.Year}-{new Random().Next(1000, 9999)}";
        }

        public async Task<CompanyDto?> RegisterCompanyAsync(RegisterCompanyRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PostAsync("/companies/register", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CompanyDto>(json, _jsonOptions);
        }

        public async Task<CompanyDto?> UpdateCompanyAsync(int id, UpdateCompanyRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PutAsync($"/companies/{id}", content);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CompanyDto>(json, _jsonOptions);
        }

        public async Task<bool> ToggleCompanyStatusAsync(int id)
        {
            var response = await _http.PatchAsync($"/companies/{id}/toggle-status", null);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("isActive", out var prop))
                return prop.GetBoolean();

            return true;
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

        public async Task<InteractionDto?> ResolveInteractionAsync(int interactionId, string resolution)
        {
            var body = new { resolution };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/interactions/{interactionId}/resolve", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<InteractionDto>(json, _jsonOptions);
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

        public async Task<FollowUpDto?> CompleteFollowUpAsync(
            int followUpId, string? outcomeNotes = null, bool logInteraction = false)
        {
            var body = new { outcomeNotes, logInteraction };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/follow-ups/{followUpId}/complete", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FollowUpDto>(json, _jsonOptions);
        }

        public async Task<FollowUpDto?> RescheduleFollowUpAsync(
            int followUpId, DateTime newScheduledAt, string? reason = null)
        {
            var body = new { newScheduledAt, reason };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/follow-ups/{followUpId}/reschedule", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FollowUpDto>(json, _jsonOptions);
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

        public async Task<RepairRequestDto?> ChangeRepairStatusAsync(
            int repairRequestId, int status, string? notes = null)
        {
            var body = new { status, notes };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/repair-requests/{repairRequestId}/status", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RepairRequestDto>(json, _jsonOptions);
        }

        public async Task<RepairRequestDto?> QuickCompleteRepairAsync(
            int repairRequestId, decimal? actualCost, decimal? partsCost, decimal? laborCost, string? technicianNotes)
        {
            var body = new { actualCost, partsCost, laborCost, technicianNotes };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync(
                $"/tenant/{CompanyId}/repair-requests/{repairRequestId}/quick-complete", content);
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

        public async Task<List<LoyaltyProgramDto>> GetLoyaltyProgramsAsync(bool activeOnly = false, int? companyId = null)
        {
            int targetCompany = companyId ?? CompanyId;
            var url = $"/loyalty-programs?companyId={targetCompany}";
            if (activeOnly) url += "&activeOnly=true";

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

        public async Task<List<SubscriptionDto>> GetSubscriptionsAsync(bool activeOnly = false, bool includeArchived = false)
        {
            var url = "/subscriptions";
            var query = new List<string>();
            if (activeOnly) query.Add("activeOnly=true");
            if (includeArchived) query.Add("includeArchived=true");
            if (query.Count > 0) url += "?" + string.Join("&", query);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<SubscriptionDto>>(json, _jsonOptions);
            return result ?? new List<SubscriptionDto>();
        }

        public async Task<SubscriptionDto?> GetSubscriptionByIdAsync(int id)
        {
            var response = await _http.GetAsync($"/subscriptions/{id}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SubscriptionDto>(json, _jsonOptions);
        }

        public async Task<SubscriptionDto?> CreateSubscriptionAsync(SubscriptionDto dto)
        {
            var body = new
            {
                subscriptionName = dto.SubscriptionName,
                pricePerMonth = dto.PricePerMonth,
                durationMonths = dto.DurationMonths,
                duration = dto.Duration,
                maxUsers = dto.MaxUsers,
                maxDevices = dto.MaxDevices,
                enableMultiBranching = dto.EnableMultiBranching,
                description = dto.Description,
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
                durationMonths = dto.DurationMonths,
                duration = dto.Duration,
                maxUsers = dto.MaxUsers,
                maxDevices = dto.MaxDevices,
                enableMultiBranching = dto.EnableMultiBranching,
                description = dto.Description,
                isActive = dto.IsActive,
                isArchived = dto.IsArchived,
                billingCycle = dto.BillingCycle
            };

            var content = ToJsonContent(body);
            var response = await _http.PutAsync($"/subscriptions/{id}", content);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SubscriptionDto>(json, _jsonOptions);
        }

        public async Task<bool> ToggleSubscriptionStatusAsync(int id)
        {
            var response = await _http.PatchAsync($"/subscriptions/{id}/toggle-status", null);
            await EnsureSuccess(response);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("isActive", out var prop))
                return prop.GetBoolean();

            return true;
        }

        public async Task ArchiveSubscriptionAsync(int id)
        {
            var response = await _http.PostAsync($"/subscriptions/{id}/archive", null);
            await EnsureSuccess(response);
        }

        public async Task RestoreSubscriptionAsync(int id)
        {
            var response = await _http.PostAsync($"/subscriptions/{id}/restore", null);
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

        public async Task<bool> AcceptTermsAsync()
        {
            var response = await _http.PostAsync("/terms/accept", null);
            await EnsureSuccess(response);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RejectTermsAsync()
        {
            try
            {
                var response = await _http.PostAsync("/terms/reject", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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

        public async Task<List<CustomerRetentionMetricsDto>> GetRetentionMetricsAsync()
        {
            var response = await _http.GetAsync($"/tenant/{CompanyId}/retention/metrics");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<CustomerRetentionMetricsDto>>(json, _jsonOptions);
            return result ?? new List<CustomerRetentionMetricsDto>();
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
        // RETENTION REQUESTS, APPROVALS & CAMPAIGNS
        // ═══════════════════════════════════════════════════════

        public async Task<List<RetentionRequestDto>> GetRetentionRequestsAsync(
            int? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var qs = new List<string>();
            if (status.HasValue) qs.Add($"status={status.Value}");
            if (fromDate.HasValue) qs.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue) qs.Add($"toDate={toDate.Value:yyyy-MM-dd}");

            var url = $"/tenant/{CompanyId}/retention/requests";
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<RetentionRequestDto>>(json, _jsonOptions) ?? new();
        }

        public async Task<RetentionRequestDto?> GetRetentionRequestAsync(int id)
        {
            var response = await _http.GetAsync($"/tenant/{CompanyId}/retention/requests/{id}");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RetentionRequestDto>(json, _jsonOptions);
        }

        public async Task<bool> CreateRetentionRequestAsync(CreateRetentionRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PostAsync($"/tenant/{CompanyId}/retention/requests", content);
            await EnsureSuccess(response);
            return true;
        }

        public async Task<bool> ApproveRetentionRequestAsync(int id, string? remarks = null)
        {
            var body = new { reviewRemarks = remarks };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync($"/tenant/{CompanyId}/retention/requests/{id}/approve", content);
            await EnsureSuccess(response);
            return true;
        }

        public async Task<bool> RejectRetentionRequestAsync(int id, string rejectionReason, string? remarks = null)
        {
            var body = new { rejectionReason, reviewRemarks = remarks };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync($"/tenant/{CompanyId}/retention/requests/{id}/reject", content);
            await EnsureSuccess(response);
            return true;
        }

        public async Task<List<RetentionCampaignDto>> GetRetentionCampaignsAsync(
            bool? isDispatched = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var qs = new List<string>();
            if (isDispatched.HasValue) qs.Add($"isDispatched={isDispatched.Value}");
            if (fromDate.HasValue) qs.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue) qs.Add($"toDate={toDate.Value:yyyy-MM-dd}");

            var url = $"/tenant/{CompanyId}/retention/campaigns";
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

            var response = await _http.GetAsync(url);
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<RetentionCampaignDto>>(json, _jsonOptions) ?? new();
        }

        public async Task<bool> DispatchRetentionEmailAsync(int id, string? customSubject = null, string? customBody = null)
        {
            var body = new { customSubject, customBody };
            var content = ToJsonContent(body);
            var response = await _http.PostAsync($"/tenant/{CompanyId}/retention/campaigns/{id}/dispatch", content);
            await EnsureSuccess(response);
            return true;
        }

        public async Task<ManualSendResultDto> SendManualRetentionEmailAsync(SendManualRetentionEmailRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PostAsync($"/tenant/{CompanyId}/retention/campaigns/manual-send", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                string msg = "Anti-Fatigue Cooldown Active";
                try
                {
                    using var doc = JsonDocument.Parse(errorJson);
                    if (doc.RootElement.TryGetProperty("message", out var m))
                        msg = m.GetString() ?? msg;
                }
                catch { }
                return new ManualSendResultDto { Success = false, InCooldown = true, Message = msg };
            }

            await EnsureSuccess(response);
            return new ManualSendResultDto { Success = true };
        }

        public async Task<List<RetentionTemplateDto>> GetRetentionTemplatesAsync()
        {
            var response = await _http.GetAsync($"/tenant/{CompanyId}/retention/templates");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<RetentionTemplateDto>>(json, _jsonOptions) ?? new();
        }

        public async Task<bool> UpdateRetentionTemplateAsync(int id, UpdateRetentionTemplateRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PutAsync($"/tenant/{CompanyId}/retention/templates/{id}", content);
            await EnsureSuccess(response);
            return true;
        }

        public async Task<RetentionSettingsDto?> GetRetentionSettingsAsync()
        {
            var response = await _http.GetAsync($"/tenant/{CompanyId}/retention/settings");
            await EnsureSuccess(response);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RetentionSettingsDto>(json, _jsonOptions);
        }

        public async Task<bool> UpdateRetentionSettingsAsync(UpdateRetentionSettingsRequestDto request)
        {
            var content = ToJsonContent(request);
            var response = await _http.PutAsync($"/tenant/{CompanyId}/retention/settings", content);
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
}
