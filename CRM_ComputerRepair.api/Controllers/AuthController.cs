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

        int companyId = user.CompanyId.HasValue && user.CompanyId.Value > 0
            ? user.CompanyId.Value
            : (request.CompanyId > 0 ? request.CompanyId : 1);

        // Verify company exists in master database
        var company = await _masterDb.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company is null)
            return Unauthorized(new { error = $"Company ID {companyId} not found." });

        if (!company.IsActive)
            return Unauthorized(new { error = $"Company '{company.CompanyName}' ({company.CompanyCode}) is currently deactivated. Contact your system administrator." });

        var passwordOk = await _users.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await _users.AccessFailedAsync(user);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        if (!user.IsActive)
            return Unauthorized(new { error = "This account has been deactivated. Contact your administrator." });

        await _users.ResetAccessFailedCountAsync(user);

        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Staff";

        var token = _jwt.CreateToken(user, roles, companyId);

        return Ok(new LoginResponse
        {
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Role = role,
            Email = user.Email ?? string.Empty,
            CompanyId = companyId,
            CompanyName = company.CompanyName,
            HasAcceptedTerms = string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase) || company.HasAcceptedTerms,
            TermsAcceptedAt = company.TermsAcceptedAt,
            Token = token
        });
    }
}