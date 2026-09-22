namespace CRM_ComputerRepair.api.Services;

/// <summary>
/// Extracts the acting user ID from a request.
/// For the demo, we use a custom header "X-User-Id" (set by ApiClient).
/// Falls back to "system".
/// </summary>
public static class UserSessionHelper
{
    public static string? GetUserId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-User-Id", out var val))
        {
            var id = val.ToString();
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }

        return "system";
    }
}