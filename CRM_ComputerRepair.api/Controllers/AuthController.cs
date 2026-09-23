using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _users;
    private readonly JwtTokenService _jwt;

    public AuthController(UserManager<User> users, JwtTokenService jwt)
    {
        _users = users;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _users.FindByNameAsync(request.Username.Trim());
        if (user is null)
            return Unauthorized(new { error = "Invalid username or password." });

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

        var token = _jwt.CreateToken(user, roles);

        return Ok(new LoginResponse
        {
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Role = role,
            Email = user.Email ?? string.Empty,
            CompanyId = 1,
            Token = token
        });
    }
}