using CRM_ComputerRepair.domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("seed-users")]
public class SeedUsersController : ControllerBase
{
    private readonly UserManager<User> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public SeedUsersController(UserManager<User> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    [HttpPost]
    public async Task<IActionResult> Seed()
    {
        var created = new List<string>();
        var skipped = new List<string>();

        var demoUsers = new[]
        {
            new { UserName = "superadmin", Email = "admin@fixory.local",       Password = "SuperAdmin@123", First = "Super",  Last = "Admin",    Role = "Super Admin" },
            new { UserName = "admin",      Email = "admin.user@fixory.local",  Password = "Admin@123",      First = "Admin",  Last = "User",     Role = "Admin" },
            new { UserName = "manager",    Email = "manager@fixory.local",     Password = "Manager@123",    First = "Manager",Last = "User",     Role = "Manager" },
            new { UserName = "staff",      Email = "staff@fixory.local",       Password = "Staff@123",      First = "Juan",   Last = "Dela Cruz",Role = "Staff" }
        };

        foreach (var d in demoUsers)
        {
            // Ensure role
            if (!await _roles.RoleExistsAsync(d.Role))
                await _roles.CreateAsync(new IdentityRole(d.Role));

            // Check existing user by username
            var existing = await _users.FindByNameAsync(d.UserName);
            if (existing != null)
            {
                skipped.Add(d.UserName);
                continue;
            }

            var user = new User
            {
                UserName = d.UserName,
                Email = d.Email,
                EmailConfirmed = true,
                FirstName = d.First,
                LastName = d.Last,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _users.CreateAsync(user, d.Password);
            if (result.Succeeded)
            {
                await _users.AddToRoleAsync(user, d.Role);
                created.Add($"{d.UserName} ({d.Role})");
            }
            else
            {
                skipped.Add($"{d.UserName} (failed: {string.Join(", ", result.Errors.Select(e => e.Description))})");
            }
        }

        return Ok(new { created, skipped, message = "Seed users complete." });
    }
}