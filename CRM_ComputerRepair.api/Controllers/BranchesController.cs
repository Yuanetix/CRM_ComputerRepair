using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using CRM_ComputerRepair.api.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/branches")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
[RequireSubscribedModule(ModuleCodes.Branching)]
public class BranchesController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly MasterCrmDbContext _master;
    private readonly UserManager<User> _userManager;
    private readonly IAuditWriter _audit;

    public BranchesController(
        ITenantDbContextFactory factory,
        MasterCrmDbContext master,
        UserManager<User> userManager,
        IAuditWriter audit)
    {
        _factory = factory;
        _master = master;
        _userManager = userManager;
        _audit = audit;
    }

    private static volatile bool _masterBranchSchemaEnsured = false;

    private async Task EnsureMasterBranchColumnsAsync()
    {
        if (_masterBranchSchemaEnsured) return;
        try
        {
            await _master.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('AspNetUsers', 'BranchId') IS NULL ALTER TABLE AspNetUsers ADD BranchId INT NULL;
IF COL_LENGTH('AspNetUsers', 'AssignedBranchName') IS NULL ALTER TABLE AspNetUsers ADD AssignedBranchName NVARCHAR(200) NULL;
");
            _masterBranchSchemaEnsured = true;
        }
        catch { }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] bool? includeInactive = false,
        [FromQuery] string? search = null)
    {
        await EnsureMasterBranchColumnsAsync();

        await using var db = await _factory.CreateAsync(companyId);

        var query = db.Branches.AsNoTracking();

        if (includeInactive != true)
        {
            query = query.Where(b => b.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b =>
                b.BranchCode.ToLower().Contains(s) ||
                b.BranchName.ToLower().Contains(s) ||
                (b.City != null && b.City.ToLower().Contains(s)) ||
                (b.ManagerName != null && b.ManagerName.ToLower().Contains(s)));
        }

        var branches = await query.OrderBy(b => b.BranchCode).ToListAsync();

        // Load staff per branch from Master Identity Users
        var tenantUsers = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId && u.IsActive && u.BranchId != null)
            .ToListAsync();

        var staffByBranch = tenantUsers
            .GroupBy(u => u.BranchId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Load record counts per branch (Customers + RepairRequests)
        var customerCounts = await db.Customers
            .AsNoTracking()
            .Where(c => c.BranchId != null)
            .GroupBy(c => c.BranchId!.Value)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count);

        var repairCounts = await db.RepairRequests
            .AsNoTracking()
            .Where(r => r.BranchId != null)
            .GroupBy(r => r.BranchId!.Value)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count);

        var dtos = branches.Select(b =>
        {
            staffByBranch.TryGetValue(b.BranchId, out int staffCount);
            customerCounts.TryGetValue(b.BranchId, out int custCount);
            repairCounts.TryGetValue(b.BranchId, out int repCount);

            return new BranchDto
            {
                BranchId = b.BranchId,
                CompanyId = b.CompanyId ?? companyId,
                BranchCode = b.BranchCode,
                BranchName = b.BranchName,
                Address = b.Address,
                City = b.City,
                StateOrProvince = b.StateOrProvince,
                PostalCode = b.PostalCode,
                Phone = b.Phone,
                Email = b.Email,
                ManagerUserId = b.ManagerUserId,
                ManagerName = b.ManagerName,
                IsActive = b.IsActive,
                StaffCount = staffCount,
                RecordsCount = custCount + repCount,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            };
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetSummaryStats(int companyId)
    {
        await EnsureMasterBranchColumnsAsync();

        await using var db = await _factory.CreateAsync(companyId);

        var branches = await db.Branches.AsNoTracking().ToListAsync();
        int activeBranches = branches.Count(b => b.IsActive);

        var branchIds = branches.Select(b => b.BranchId).ToList();

        // Total Staff across active branches
        int totalStaff = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId && u.IsActive && u.BranchId != null && branchIds.Contains(u.BranchId.Value))
            .CountAsync();

        // Active pipeline: active repair requests (pending, in progress, approved) + active customers
        int activeRepairs = await db.RepairRequests
            .AsNoTracking()
            .Where(r => r.Status != RepairStatus.Completed && r.Status != RepairStatus.Rejected)
            .CountAsync();

        int activeCustomers = await db.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .CountAsync();

        // Closed revenue: completed repair actual costs or completed payments
        decimal closedRevenue = await db.RepairRequests
            .AsNoTracking()
            .Where(r => r.Status == RepairStatus.Completed && r.ActualCost.HasValue)
            .SumAsync(r => r.ActualCost!.Value);

        if (closedRevenue == 0)
        {
            closedRevenue = await db.Payments
                .AsNoTracking()
                .Where(p => p.IsPaid && !p.IsVoid)
                .SumAsync(p => p.Amount);
        }

        return Ok(new BranchSummaryStatsDto
        {
            TotalActiveBranches = activeBranches,
            TotalStaff = totalStaff,
            ActivePipelineRecords = activeRepairs + activeCustomers,
            TotalClosedRevenue = closedRevenue
        });
    }

    [HttpGet("{branchId:int}")]
    public async Task<IActionResult> GetById(int companyId, int branchId)
    {
        await EnsureMasterBranchColumnsAsync();

        await using var db = await _factory.CreateAsync(companyId);
        var b = await db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId);
        if (b == null)
            return NotFound(new { message = $"Branch with ID {branchId} was not found." });

        int staffCount = await _userManager.Users
            .AsNoTracking()
            .CountAsync(u => u.CompanyId == companyId && u.IsActive && u.BranchId == branchId);

        int recordsCount = await db.Customers.CountAsync(c => c.BranchId == branchId) +
                           await db.RepairRequests.CountAsync(r => r.BranchId == branchId);

        return Ok(new BranchDto
        {
            BranchId = b.BranchId,
            CompanyId = b.CompanyId ?? companyId,
            BranchCode = b.BranchCode,
            BranchName = b.BranchName,
            Address = b.Address,
            City = b.City,
            StateOrProvince = b.StateOrProvince,
            PostalCode = b.PostalCode,
            Phone = b.Phone,
            Email = b.Email,
            ManagerUserId = b.ManagerUserId,
            ManagerName = b.ManagerName,
            IsActive = b.IsActive,
            StaffCount = staffCount,
            RecordsCount = recordsCount,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> Create(int companyId, [FromBody] CreateBranchRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BranchCode))
            return BadRequest(new { message = "Branch Code is required." });

        if (string.IsNullOrWhiteSpace(req.BranchName))
            return BadRequest(new { message = "Branch Name is required." });

        await using var db = await _factory.CreateAsync(companyId);

        string normalizedCode = req.BranchCode.Trim().ToUpperInvariant();
        bool exists = await db.Branches.AnyAsync(b => b.BranchCode.ToUpper() == normalizedCode);
        if (exists)
            return BadRequest(new { message = $"Branch code '{req.BranchCode}' is already in use." });

        string? managerName = null;
        if (!string.IsNullOrWhiteSpace(req.ManagerUserId))
        {
            var mgr = await _userManager.FindByIdAsync(req.ManagerUserId);
            if (mgr != null)
            {
                managerName = mgr.FullName;
            }
        }

        var branch = new Branch
        {
            CompanyId = companyId,
            BranchCode = normalizedCode,
            BranchName = req.BranchName.Trim(),
            Address = req.Address?.Trim(),
            City = req.City?.Trim(),
            StateOrProvince = req.StateOrProvince?.Trim(),
            PostalCode = req.PostalCode?.Trim(),
            Phone = req.Phone?.Trim(),
            Email = req.Email?.Trim(),
            ManagerUserId = req.ManagerUserId,
            ManagerName = managerName,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        // Also assign user's branch in identity if manager was set
        if (!string.IsNullOrWhiteSpace(req.ManagerUserId))
        {
            var mgr = await _userManager.FindByIdAsync(req.ManagerUserId);
            if (mgr != null)
            {
                mgr.BranchId = branch.BranchId;
                mgr.AssignedBranchName = branch.BranchName;
                await _userManager.UpdateAsync(mgr);
            }
        }

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Create",
            "Branch",
            branch.BranchId.ToString(),
            $"Created branch '{branch.BranchCode}' - '{branch.BranchName}' in Company {companyId}.");

        return CreatedAtAction(nameof(GetById), new { companyId, branchId = branch.BranchId }, new BranchDto
        {
            BranchId = branch.BranchId,
            CompanyId = companyId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            Address = branch.Address,
            City = branch.City,
            StateOrProvince = branch.StateOrProvince,
            PostalCode = branch.PostalCode,
            Phone = branch.Phone,
            Email = branch.Email,
            ManagerUserId = branch.ManagerUserId,
            ManagerName = branch.ManagerName,
            IsActive = branch.IsActive,
            StaffCount = string.IsNullOrEmpty(branch.ManagerUserId) ? 0 : 1,
            RecordsCount = 0,
            CreatedAt = branch.CreatedAt,
            UpdatedAt = branch.UpdatedAt
        });
    }

    [HttpPut("{branchId:int}")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> Update(int companyId, int branchId, [FromBody] UpdateBranchRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BranchCode))
            return BadRequest(new { message = "Branch Code is required." });

        if (string.IsNullOrWhiteSpace(req.BranchName))
            return BadRequest(new { message = "Branch Name is required." });

        await using var db = await _factory.CreateAsync(companyId);

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {branchId} was not found." });

        string normalizedCode = req.BranchCode.Trim().ToUpperInvariant();
        bool codeConflict = await db.Branches.AnyAsync(b => b.BranchId != branchId && b.BranchCode.ToUpper() == normalizedCode);
        if (codeConflict)
            return BadRequest(new { message = $"Branch code '{req.BranchCode}' is already in use by another branch." });

        string? managerName = null;
        if (!string.IsNullOrWhiteSpace(req.ManagerUserId))
        {
            var mgr = await _userManager.FindByIdAsync(req.ManagerUserId);
            if (mgr != null)
            {
                managerName = mgr.FullName;
                mgr.BranchId = branch.BranchId;
                mgr.AssignedBranchName = req.BranchName.Trim();
                await _userManager.UpdateAsync(mgr);
            }
        }

        branch.BranchCode = normalizedCode;
        branch.BranchName = req.BranchName.Trim();
        branch.Address = req.Address?.Trim();
        branch.City = req.City?.Trim();
        branch.StateOrProvince = req.StateOrProvince?.Trim();
        branch.PostalCode = req.PostalCode?.Trim();
        branch.Phone = req.Phone?.Trim();
        branch.Email = req.Email?.Trim();
        branch.ManagerUserId = req.ManagerUserId;
        branch.ManagerName = managerName;
        branch.IsActive = req.IsActive;
        branch.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Update",
            "Branch",
            branch.BranchId.ToString(),
            $"Updated branch '{branch.BranchCode}' - '{branch.BranchName}'.");

        int staffCount = await _userManager.Users
            .AsNoTracking()
            .CountAsync(u => u.CompanyId == companyId && u.IsActive && u.BranchId == branchId);

        int recordsCount = await db.Customers.CountAsync(c => c.BranchId == branchId) +
                           await db.RepairRequests.CountAsync(r => r.BranchId == branchId);

        return Ok(new BranchDto
        {
            BranchId = branch.BranchId,
            CompanyId = companyId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            Address = branch.Address,
            City = branch.City,
            StateOrProvince = branch.StateOrProvince,
            PostalCode = branch.PostalCode,
            Phone = branch.Phone,
            Email = branch.Email,
            ManagerUserId = branch.ManagerUserId,
            ManagerName = branch.ManagerName,
            IsActive = branch.IsActive,
            StaffCount = staffCount,
            RecordsCount = recordsCount,
            CreatedAt = branch.CreatedAt,
            UpdatedAt = branch.UpdatedAt
        });
    }

    [HttpPatch("{branchId:int}/toggle-active")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> ToggleActive(int companyId, int branchId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {branchId} was not found." });

        branch.IsActive = !branch.IsActive;
        branch.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        string actionDesc = branch.IsActive ? "Reactivated" : "Deactivated";
        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "StatusChange",
            "Branch",
            branch.BranchId.ToString(),
            $"{actionDesc} branch '{branch.BranchCode}' ({branch.BranchName}).");

        return Ok(new
        {
            branchId = branch.BranchId,
            isActive = branch.IsActive,
            message = $"Branch '{branch.BranchName}' has been successfully {(branch.IsActive ? "reactivated" : "deactivated")}."
        });
    }
}
