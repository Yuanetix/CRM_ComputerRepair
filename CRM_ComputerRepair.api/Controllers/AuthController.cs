using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _users;
    private readonly JwtTokenService _jwt;
    private readonly MasterCrmDbContext _masterDb;

    public AuthController(UserManager<User> users, JwtTokenService jwt, MasterCrmDbContext masterDb)
    {
        _users = users;
        _jwt = jwt;
        _masterDb = masterDb;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _users.FindByNameAsync(request.Username.Trim());
        if (user is null)
            return Unauthorized(new { error = "Invalid username or password." });

        if (!user.IsActive)
            return Unauthorized(new { error = "This account has been deactivated. Contact your administrator." });

        var passwordOk = await _users.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await _users.AccessFailedAsync(user);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        await _users.ResetAccessFailedCountAsync(user);

        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Staff";
        var isSuperAdmin = string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase);

        if (isSuperAdmin)
        {
            var superToken = _jwt.CreateToken(user, roles, 0);
            return Ok(new LoginResponse
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Role = role,
                Email = user.Email ?? string.Empty,
                CompanyId = 0,
                CompanyName = "System Administration",
                HasAcceptedTerms = true,
                TermsAcceptedAt = DateTime.UtcNow,
                Token = superToken,
                SubscribedModules = ModuleCodes.All.ToList(),
                BranchId = null,
                BranchName = null
            });
        }

        int companyId = user.CompanyId.HasValue && user.CompanyId.Value > 0
            ? user.CompanyId.Value
            : (request.CompanyId > 0 ? request.CompanyId : 1);

        // Verify company exists in master database
        var company = await _masterDb.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company is null)
            return Unauthorized(new { error = $"Company ID {companyId} not found." });

        if (!company.IsActive)
            return Unauthorized(new { error = $"Company '{company.CompanyName}' ({company.CompanyCode}) is currently deactivated. Contact your system administrator." });

        var token = _jwt.CreateToken(user, roles, companyId);

        var activeModules = new List<string>();
        if (company.Subscription != null &&
            company.Subscription.IsActive &&
            !company.Subscription.IsArchived &&
            company.Subscription.EndDate >= DateTime.UtcNow)
        {
            var planModules = company.Subscription.Plan != null && company.Subscription.Plan.IsActive && !company.Subscription.Plan.IsArchived
                ? company.Subscription.Plan.PlanModules
                    .Where(pm => pm.Module != null && pm.Module.IsActive)
                    .Select(pm => pm.Module!.ModuleCode)
                : Enumerable.Empty<string>();

            var addonModules = company.Subscription.SubscriptionModules
                .Where(sm => sm.IsActive && sm.Module != null && sm.Module.IsActive)
                .Select(sm => sm.Module!.ModuleCode);

            activeModules = planModules.Union(addonModules, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return Ok(new LoginResponse
        {
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Role = role,
            Email = user.Email ?? string.Empty,
            CompanyId = companyId,
            CompanyName = company.CompanyName,
            HasAcceptedTerms = company.HasAcceptedTerms,
            TermsAcceptedAt = company.TermsAcceptedAt,
            Token = token,
            SubscribedModules = activeModules,
            BranchId = user.BranchId,
            BranchName = user.AssignedBranchName
        });
    }
}