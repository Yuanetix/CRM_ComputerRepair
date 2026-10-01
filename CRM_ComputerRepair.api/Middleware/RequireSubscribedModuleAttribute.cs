using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CRM_ComputerRepair.api.Middleware;

/// <summary>
/// Enforces subscription-driven module access at the backend route/controller level.
/// Flow:
/// 1. Authenticate user.
/// 2. If user is in role "Super Admin", access is granted unconditionally (Super Admin manages the platform).
/// 3. Identify tenant/company from route ({companyId}), header (X-Company-Id), or JWT claim (company_id).
/// 4. Validate tenant isolation: Non-SuperAdmin cannot access another company's endpoints.
/// 5. Load active company subscription and its active subscribed modules from master_db.
/// 6. Determine if the requested module is active in the company subscription.
/// 7. Allow or deny (HTTP 403 Forbidden with descriptive JSON).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireSubscribedModuleAttribute : Attribute, IAsyncActionFilter
{
    public string[] ModuleCodes { get; }
    public string ModuleCode => ModuleCodes.Length > 0 ? ModuleCodes[0] : string.Empty;

    public RequireSubscribedModuleAttribute(params string[] moduleCodes)
    {
        ModuleCodes = moduleCodes ?? Array.Empty<string>();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        // 1. Super Admin bypass (Super Admin has unrestricted platform access)
        if (user.IsInRole("Super Admin"))
        {
            await next();
            return;
        }

        // 2. Identify tenant/company ID
        int companyId = 0;
        if (context.RouteData.Values.TryGetValue("companyId", out var routeVal) &&
            int.TryParse(routeVal?.ToString(), out int rId))
        {
            companyId = rId;
        }
        else if (httpContext.Request.Headers.TryGetValue("X-Company-Id", out var hVal) &&
                 int.TryParse(hVal.ToString(), out int hId))
        {
            companyId = hId;
        }
        else
        {
            var claimVal = user.FindFirst("company_id")?.Value;
            if (int.TryParse(claimVal, out int cId))
                companyId = cId;
        }

        if (companyId <= 0)
        {
            context.Result = new ObjectResult(new
            {
                error = "Tenant identification failed. A valid company identifier is required for module access.",
                status = 403
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // 3. Tenant Data Isolation check: prevent cross-tenant parameter tampering
        var userCompClaim = user.FindFirst("company_id")?.Value;
        if (int.TryParse(userCompClaim, out int userCompId) && userCompId > 0 && userCompId != companyId)
        {
            context.Result = new ObjectResult(new
            {
                error = $"Cross-tenant access forbidden. User belongs to Company ID {userCompId} and cannot access Company ID {companyId}.",
                status = 403
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // 4. Resolve MasterCrmDbContext
        var masterDb = httpContext.RequestServices.GetRequiredService<MasterCrmDbContext>();

        // 5. Query company and subscription with its active modules
        // 5. Query company and subscription with its plan, plan modules, and active add-ons
        var company = await masterDb.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company == null)
        {
            context.Result = new ObjectResult(new
            {
                error = $"Company ID {companyId} not found.",
                status = 403
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (!company.IsActive)
        {
            context.Result = new ObjectResult(new
            {
                error = $"Company '{company.CompanyName}' ({company.CompanyCode}) has been deactivated.",
                status = 403
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // 6. Verify subscription is active
        var subscription = company.Subscription;
        if (subscription == null || !subscription.IsActive || subscription.IsArchived || (subscription.EndDate < DateTime.UtcNow))
        {
            context.Result = new ObjectResult(new
            {
                error = $"Company '{company.CompanyName}' does not have an active subscription.",
                status = 403,
                companyId = companyId
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        bool Matches(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            foreach (var reqCode in ModuleCodes)
            {
                if (string.Equals(code, reqCode, StringComparison.OrdinalIgnoreCase)) return true;
                if ((string.Equals(reqCode, "ACTIONS", StringComparison.OrdinalIgnoreCase) || string.Equals(reqCode, "RETENTION", StringComparison.OrdinalIgnoreCase)) &&
                    (string.Equals(code, "ACTIONS", StringComparison.OrdinalIgnoreCase) || string.Equals(code, "RETENTION", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        // 7. Verify the requested module is included in active plan OR active add-ons
        bool isInPlan = subscription.Plan != null &&
                        subscription.Plan.IsActive &&
                        !subscription.Plan.IsArchived &&
                        subscription.Plan.PlanModules.Any(pm =>
                            pm.Module != null &&
                            pm.Module.IsActive &&
                            Matches(pm.Module.ModuleCode));

        bool isInAddons = subscription.SubscriptionModules.Any(sm =>
            sm.IsActive &&
            sm.Module != null &&
            sm.Module.IsActive &&
            Matches(sm.Module.ModuleCode));

        bool isSubscribed = isInPlan || isInAddons;

        if (!isSubscribed)
        {
            string planName = subscription.Plan?.PlanName ?? subscription.SubscriptionName ?? "Custom";
            string reqText = string.Join(" or ", ModuleCodes);
            context.Result = new ObjectResult(new
            {
                error = $"Access denied. Requires module '{reqText}', which is not included in company '{company.CompanyName}' active subscription (Current Plan: {planName}).",
                requiredModule = reqText,
                companyId = companyId,
                status = 403
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
