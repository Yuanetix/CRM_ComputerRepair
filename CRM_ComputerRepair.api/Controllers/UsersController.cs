using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<User> _users;
    private readonly IAuditWriter _audit;

    public UsersController(UserManager<User> users, IAuditWriter audit)
    {
        _users = users;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
    {
        var query = _users.Users.AsNoTracking();
        if (activeOnly == true)
            query = query.Where(u => u.IsActive);

        var list = await query.OrderBy(u => u.UserName).ToListAsync();

        var result = new List<UserSummaryDto>();
        foreach (var u in list)
        {
            var roles = await _users.GetRolesAsync(u);
            result.Add(new UserSummaryDto
            {
                Id = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();

        var roles = await _users.GetRolesAsync(u);

        return Ok(new UserSummaryDto
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            Roles = roles.ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Role))
            request.Role = "Staff";

        var user = new User
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Ensure the role exists
        var role = request.Role.Trim();
        if (!await RoleExistsAsync(role))
            await CreateRoleAsync(role);

        await _users.AddToRoleAsync(user, role);

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Create", "User", user.Id, $"{user.UserName} ({role})");

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email?.Trim();
        user.UserName = request.UserName?.Trim() ?? user.UserName;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);

        // Role change
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = request.Role.Trim();
            var currentRoles = await _users.GetRolesAsync(user);

            foreach (var r in currentRoles)
                await _users.RemoveFromRoleAsync(user, r);

            if (!await RoleExistsAsync(role))
                await CreateRoleAsync(role);

            await _users.AddToRoleAsync(user, role);
        }

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Update", "User", user.Id, $"{user.UserName}");

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Deactivate", "User", user.Id, user.UserName ?? "");

        return Ok(new { message = $"User {user.UserName} deactivated." });
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> Restore(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Restore", "User", user.Id, user.UserName ?? "");

        return Ok(new { message = $"User {user.UserName} restored." });
    }

    private async Task<bool> RoleExistsAsync(string role)
    {
        // Use the role manager if available
        var roleManager = HttpContext.RequestServices.GetService<RoleManager<IdentityRole>>();
        if (roleManager == null) return false;
        return await roleManager.RoleExistsAsync(role);
    }

    private async Task CreateRoleAsync(string role)
    {
        var roleManager = HttpContext.RequestServices.GetService<RoleManager<IdentityRole>>();
        if (roleManager == null) return;
        await roleManager.CreateAsync(new IdentityRole(role));
    }
}