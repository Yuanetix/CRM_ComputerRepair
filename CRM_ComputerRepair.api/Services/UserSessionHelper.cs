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
        var sub = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub)) return sub;

        if (context.Request.Headers.TryGetValue("X-User-Id", out var val))
        {
            var id = val.ToString();
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }

        return "system";
    }

    /// <summary>Company id from the X-Company-Id header or JWT claim (seeded demo users default to CompanyId = 1).</summary>
    public static int GetCompanyId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Company-Id", out var headerVal) &&
            int.TryParse(headerVal.ToString(), out var headerId) && headerId > 0)
        {
            return headerId;
        }

        var claim = context.User.FindFirst("CompanyId")?.Value;
        return int.TryParse(claim, out var id) && id > 0 ? id : 1;
    }
}