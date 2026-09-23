using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("admin-accounts")]
[Authorize(Roles = "Super Admin")]
public class AdminAccountsController : ControllerBase
{
    private readonly UserManager<User> _users;

    public AdminAccountsController(UserManager<User> users)
    {
        _users = users;
    }

    /// <summary>
    /// Returns users whose role is either "Admin" or "Super Admin".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var adminRoleName = "Admin";
        var superRoleName = "Super Admin";

        var adminRole = await _users.GetUsersInRoleAsync(adminRoleName);
        var superAdmins = await _users.GetUsersInRoleAsync(superRoleName);

        var combined = adminRole.Concat(superAdmins)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();

        var result = new List<UserSummaryDto>();
        foreach (var u in combined)
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

        return Ok(result.OrderBy(x => x.UserName).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();

        var roles = await _users.GetRolesAsync(u);

        if (!roles.Contains("Admin") && !roles.Contains("Super Admin"))
            return NotFound();

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
}