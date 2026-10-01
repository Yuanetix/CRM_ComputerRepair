using CRM_ComputerRepair.domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CRM_ComputerRepair.api.Services;

public static class BranchScopeHelper
{
    public static async Task<(int? BranchId, bool IsAllowed)> ResolveBranchScopeAsync(
        HttpContext context,
        UserManager<User> userManager,
        int? requestedBranchId = null)
    {
        // Check query param first, then header X-Branch-Id
        int? branchId = requestedBranchId;
        if (!branchId.HasValue && context.Request.Headers.TryGetValue("X-Branch-Id", out var hVal) && int.TryParse(hVal, out int parsed))
        {
            branchId = parsed;
        }

        // Super Admin and Company Admin have full access across all branches or scoped
        if (context.User.IsInRole("Super Admin") || context.User.IsInRole("Admin"))
        {
            return (branchId, true);
        }

        // Branch Managers and Regular Staff are strictly locked to their assigned branch
        int? userBranchId = null;
        var branchClaim = context.User.FindFirst("BranchId")?.Value;
        if (int.TryParse(branchClaim, out int bClaimId) && bClaimId > 0)
        {
            userBranchId = bClaimId;
        }
        else
        {
            var userId = UserSessionHelper.GetUserId(context);
            if (!string.IsNullOrEmpty(userId) && userId != "system")
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user != null && user.BranchId.HasValue && user.BranchId.Value > 0)
                {
                    userBranchId = user.BranchId.Value;
                }
            }
        }

        if (userBranchId.HasValue)
        {
            // If a non-admin requests a branch other than their assigned one, deny access
            if (branchId.HasValue && branchId.Value != userBranchId.Value)
            {
                return (null, false);
            }
            // Otherwise, enforce their assigned branch (even if no branch was requested)
            return (userBranchId.Value, true);
        }

        return (branchId, true);
    }
}
