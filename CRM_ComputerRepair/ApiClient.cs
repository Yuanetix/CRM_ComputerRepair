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

        // Hardcoded tenant for the exam demo (Company 1 = First Repair Shop)
        private const int CompanyId = 1;

        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClient()
        {
            // Accept self-signed dev certificate
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

        // ─────────── CUSTOMER ───────────

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

        // ─────────── HELPERS ───────────

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

    // DTO matching the API's JSON response
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

        // Display helper
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}