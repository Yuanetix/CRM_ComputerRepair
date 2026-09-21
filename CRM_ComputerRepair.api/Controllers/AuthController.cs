using CRM_ComputerRepair.api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private static readonly Dictionary<string, DemoUser> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["superadmin"] = new("superadmin", "Super Admin", "Super Admin", "admin@fixory.local", "SuperAdmin@123", 1),
        ["admin"] = new("admin", "Admin User", "Admin", "admin.user@fixory.local", "Admin@123", 1),
        ["manager"] = new("manager", "Manager User", "Manager", "manager@fixory.local", "Manager@123", 1),
        ["staff"] = new("staff", "Juan Dela Cruz", "Staff", "staff@fixory.local", "Staff@123", 1),
    };

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Users.TryGetValue(request.Username, out var user) ||
            user.Password != request.Password)
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        return Ok(new LoginResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            Email = user.Email,
            CompanyId = user.CompanyId
        });
    }
}

public record DemoUser(
    string UserId,
    string FullName,
    string Role,
    string Email,
    string Password,
    int CompanyId)
{
    public string Username => UserId;
}