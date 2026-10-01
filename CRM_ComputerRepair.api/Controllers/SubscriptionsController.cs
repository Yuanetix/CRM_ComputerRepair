using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("subscriptions")]
[Authorize(Roles = "Super Admin,Admin")]
public class SubscriptionsController : ControllerBase
{
    private readonly MasterCrmDbContext _db;
    private readonly IAuditWriter _audit;

    public SubscriptionsController(MasterCrmDbContext db, IAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    // ═══════════════════════════════════════════════════════
    // 1. CONFIGURABLE SUBSCRIPTION PLANS
    // ═══════════════════════════════════════════════════════

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans([FromQuery] bool? activeOnly = null, [FromQuery] bool includeArchived = false)
    {
        var query = _db.SubscriptionPlans
            .Include(p => p.PlanModules)
                .ThenInclude(pm => pm.Module)
            .AsNoTracking();

        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsArchived);

        var plans = await query
            .OrderBy(x => x.SubscriptionPlanId)
            .ToListAsync();

        var companySubCounts = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.SubscriptionPlanId.HasValue && s.IsActive)
            .GroupBy(s => s.SubscriptionPlanId!.Value)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count);

        var dtos = plans.Select(p => MapToPlanDto(p, companySubCounts.TryGetValue(p.SubscriptionPlanId, out int cnt) ? cnt : 0)).ToList();
        return Ok(dtos);
    }

    [HttpGet("plans/{id:int}")]
    public async Task<IActionResult> GetPlanById(int id)
    {
        var p = await _db.SubscriptionPlans
            .Include(x => x.PlanModules)
                .ThenInclude(pm => pm.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriptionPlanId == id);

        if (p is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        int activeSubs = await _db.Subscriptions
            .AsNoTracking()
            .CountAsync(s => s.SubscriptionPlanId == id && s.IsActive);

        return Ok(MapToPlanDto(p, activeSubs));
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var code = request.PlanCode.Trim().ToUpperInvariant();
        if (await _db.SubscriptionPlans.AnyAsync(x => x.PlanCode.ToUpper() == code))
        {
            return BadRequest(new { error = $"Plan with code '{code}' already exists." });
        }

        var plan = new SubscriptionPlan
        {
            PlanCode = code,
            PlanName = request.PlanName.Trim(),
            Description = request.Description?.Trim(),
            PricePerMonth = request.Price,
            BillingCycle = request.BillingInterval ?? "Monthly",
            Status = "Active",
            IsActive = true,
            IsArchived = false,
            MaxUsers = request.MaxUsers > 0 ? request.MaxUsers : 10,
            MaxBranches = request.MaxBranches > 0 ? request.MaxBranches : 1,
            MaxDevices = request.MaxDevices > 0 ? request.MaxDevices : 500,
            CreatedAt = DateTime.UtcNow
        };

        if (request.ModuleCodes != null && request.ModuleCodes.Count > 0)
        {
            var upperCodes = request.ModuleCodes.Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
            var modules = await _db.AppModules.Where(m => upperCodes.Contains(m.ModuleCode.ToUpper())).ToListAsync();
            foreach (var mod in modules)
            {
                plan.PlanModules.Add(new PlanModule
                {
                    ModuleId = mod.ModuleId,
                    Plan = plan
                });
            }
        }

        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("CreatePlan", "SubscriptionPlan", plan.SubscriptionPlanId.ToString(),
            $"Created subscription plan '{plan.PlanName}' ({plan.PlanCode}) at ₱{plan.PricePerMonth:N2}/mo with {plan.PlanModules.Count} module(s).");

        return CreatedAtAction(nameof(GetPlanById), new { id = plan.SubscriptionPlanId }, MapToPlanDto(plan, 0));
    }

    [HttpPut("plans/{id:int}")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdatePlanRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var plan = await _db.SubscriptionPlans
            .Include(p => p.PlanModules)
            .FirstOrDefaultAsync(p => p.SubscriptionPlanId == id);

        if (plan is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        plan.PlanName = request.PlanName.Trim();
        plan.Description = request.Description?.Trim();
        plan.PricePerMonth = request.Price;
        plan.BillingCycle = request.BillingInterval ?? plan.BillingCycle;
        plan.Status = request.Status ?? plan.Status;
        plan.MaxUsers = request.MaxUsers > 0 ? request.MaxUsers : 1;
        plan.MaxBranches = request.MaxBranches > 0 ? request.MaxBranches : 1;
        plan.MaxDevices = request.MaxDevices > 0 ? request.MaxDevices : 1;
        plan.UpdatedAt = DateTime.UtcNow;

        if (request.ModuleCodes != null)
        {
            var upperCodes = request.ModuleCodes.Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
            var allModules = await _db.AppModules.ToListAsync();
            var targetModules = allModules.Where(m => upperCodes.Contains(m.ModuleCode.ToUpperInvariant())).ToList();

            // Remove unselected
            var toRemove = plan.PlanModules.Where(pm => !targetModules.Any(tm => tm.ModuleId == pm.ModuleId)).ToList();
            foreach (var r in toRemove) plan.PlanModules.Remove(r);

            // Add newly selected
            foreach (var tm in targetModules)
            {
                if (!plan.PlanModules.Any(pm => pm.ModuleId == tm.ModuleId))
                {
                    plan.PlanModules.Add(new PlanModule
                    {
                        SubscriptionPlanId = plan.SubscriptionPlanId,
                        ModuleId = tm.ModuleId
                    });
                }
            }
        }

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("UpdatePlan", "SubscriptionPlan", plan.SubscriptionPlanId.ToString(),
            $"Updated subscription plan '{plan.PlanName}' ({plan.PlanCode}). Price: ₱{plan.PricePerMonth:N2}/mo, Modules: {plan.PlanModules.Count}.");

        return await GetPlanById(id);
    }

    [HttpPatch("plans/{id:int}/toggle-status")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> TogglePlanStatus(int id)
    {
        var item = await _db.SubscriptionPlans.FirstOrDefaultAsync(x => x.SubscriptionPlanId == id);
        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.IsActive = !item.IsActive;
        item.Status = item.IsActive ? "Active" : "Inactive";
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        string action = item.IsActive ? "Activated" : "Deactivated";
        await _audit.WriteAsync(action, "SubscriptionPlan", item.SubscriptionPlanId.ToString(),
            $"{action} subscription plan '{item.PlanName}'.");

        return Ok(new
        {
            message = $"Subscription plan '{item.PlanName}' is now {action.ToLowerInvariant()}.",
            isActive = item.IsActive
        });
    }

    [HttpPost("plans/{id:int}/archive")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> ArchivePlan(int id)
    {
        var item = await _db.SubscriptionPlans.FirstOrDefaultAsync(x => x.SubscriptionPlanId == id);
        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.IsArchived = true;
        item.IsActive = false;
        item.Status = "Archived";
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("ArchivePlan", "SubscriptionPlan", item.SubscriptionPlanId.ToString(),
            $"Archived subscription plan '{item.PlanName}'.");

        return Ok(new { message = $"Subscription plan '{item.PlanName}' has been archived." });
    }

    [HttpPost("plans/{id:int}/restore")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> RestorePlan(int id)
    {
        var item = await _db.SubscriptionPlans.FirstOrDefaultAsync(x => x.SubscriptionPlanId == id);
        if (item is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        item.IsArchived = false;
        item.IsActive = true;
        item.Status = "Active";
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("RestorePlan", "SubscriptionPlan", item.SubscriptionPlanId.ToString(),
            $"Restored subscription plan '{item.PlanName}'.");

        return Ok(new { message = $"Subscription plan '{item.PlanName}' has been restored and activated." });
    }

    // ═══════════════════════════════════════════════════════
    // 2. LEGACY / BACKWARD-COMPATIBLE PLAN ENDPOINTS
    // ═══════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null, [FromQuery] bool includeArchived = false)
    {
        // First try to return SubscriptionPlans mapped to SubscriptionPlanDetailDto for backwards compatibility
        var query = _db.SubscriptionPlans
            .Include(p => p.PlanModules)
                .ThenInclude(pm => pm.Module)
            .AsNoTracking();

        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);

        if (activeOnly == true)
            query = query.Where(x => x.IsActive && !x.IsArchived);

        var list = await query
            .OrderBy(x => x.SubscriptionPlanId)
            .ToListAsync();

        if (list.Count > 0)
        {
            var companySubCounts = await _db.Subscriptions
                .AsNoTracking()
                .Where(s => s.SubscriptionPlanId.HasValue && s.IsActive)
                .GroupBy(s => s.SubscriptionPlanId!.Value)
                .Select(g => new { PlanId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Count);

            var planDtos = list.Select(p => new SubscriptionPlanDetailDto
            {
                SubscriptionId = p.SubscriptionPlanId,
                SubscriptionName = p.PlanName,
                PricePerMonth = p.PricePerMonth,
                DurationMonths = 1,
                Duration = p.BillingCycle ?? "Monthly",
                MaxUsers = p.MaxUsers,
                MaxDevices = p.MaxDevices,
                EnableMultiBranching = p.PlanModules.Any(m => m.Module?.ModuleCode.Equals("BRANCHING", StringComparison.OrdinalIgnoreCase) == true),
                Description = p.Description,
                IsActive = p.IsActive,
                IsArchived = p.IsArchived,
                BillingCycle = p.BillingCycle,
                StartDate = p.CreatedAt,
                EndDate = p.CreatedAt.AddYears(1),
                SubscribedCompaniesCount = companySubCounts.TryGetValue(p.SubscriptionPlanId, out int cnt) ? cnt : 0
            }).ToList();

            return Ok(planDtos);
        }

        // Fallback to legacy Subscriptions table
        var subQuery = _db.Subscriptions.AsNoTracking();
        if (!includeArchived) subQuery = subQuery.Where(x => !x.IsArchived);
        if (activeOnly == true) subQuery = subQuery.Where(x => x.IsActive && !x.IsArchived);

        var subList = await subQuery.ToListAsync();
        return Ok(subList.Select(s => new SubscriptionPlanDetailDto
        {
            SubscriptionId = s.SubscriptionId,
            CompanyId = s.CompanyId,
            SubscriptionName = s.SubscriptionName,
            PricePerMonth = s.PricePerMonth,
            DurationMonths = s.DurationMonths,
            Duration = s.Duration ?? $"{s.DurationMonths} Month(s)",
            MaxUsers = s.MaxUsers,
            MaxDevices = s.MaxDevices,
            EnableMultiBranching = s.EnableMultiBranching,
            Description = s.Description,
            IsActive = s.IsActive,
            IsArchived = s.IsArchived,
            BillingCycle = s.BillingCycle,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            SubscribedCompaniesCount = 0
        }).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var plan = await _db.SubscriptionPlans
            .Include(p => p.PlanModules)
                .ThenInclude(pm => pm.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SubscriptionPlanId == id);

        if (plan != null)
        {
            int count = await _db.Subscriptions.CountAsync(s => s.SubscriptionPlanId == id && s.IsActive);
            return Ok(new SubscriptionPlanDetailDto
            {
                SubscriptionId = plan.SubscriptionPlanId,
                SubscriptionName = plan.PlanName,
                PricePerMonth = plan.PricePerMonth,
                DurationMonths = 1,
                Duration = plan.BillingCycle ?? "Monthly",
                MaxUsers = plan.MaxUsers,
                MaxDevices = plan.MaxDevices,
                EnableMultiBranching = plan.PlanModules.Any(m => m.Module?.ModuleCode.Equals("BRANCHING", StringComparison.OrdinalIgnoreCase) == true),
                Description = plan.Description,
                IsActive = plan.IsActive,
                IsArchived = plan.IsArchived,
                BillingCycle = plan.BillingCycle,
                StartDate = plan.CreatedAt,
                EndDate = plan.CreatedAt.AddYears(1),
                SubscribedCompaniesCount = count
            });
        }

        var s = await _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.SubscriptionId == id);
        if (s is null) return NotFound(new { error = $"Subscription plan {id} not found." });

        return Ok(new SubscriptionPlanDetailDto
        {
            SubscriptionId = s.SubscriptionId,
            CompanyId = s.CompanyId,
            SubscriptionName = s.SubscriptionName,
            PricePerMonth = s.PricePerMonth,
            DurationMonths = s.DurationMonths,
            Duration = s.Duration ?? $"{s.DurationMonths} Month(s)",
            MaxUsers = s.MaxUsers,
            MaxDevices = s.MaxDevices,
            EnableMultiBranching = s.EnableMultiBranching,
            Description = s.Description,
            IsActive = s.IsActive,
            IsArchived = s.IsArchived,
            BillingCycle = s.BillingCycle,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            SubscribedCompaniesCount = 0
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest request)
    {
        return await CreatePlan(new CreatePlanRequest
        {
            PlanCode = request.SubscriptionName.Replace(" ", "_").ToUpperInvariant(),
            PlanName = request.SubscriptionName,
            Price = request.PricePerMonth,
            Description = request.Description,
            MaxUsers = request.MaxUsers,
            MaxDevices = request.MaxDevices,
            BillingInterval = request.BillingCycle ?? "Monthly"
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSubscriptionRequest request)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.SubscriptionPlanId == id);
        if (plan != null)
        {
            return await UpdatePlan(id, new UpdatePlanRequest
            {
                PlanName = request.SubscriptionName,
                Price = request.PricePerMonth,
                Description = request.Description,
                MaxUsers = request.MaxUsers,
                MaxDevices = request.MaxDevices,
                BillingInterval = request.BillingCycle ?? "Monthly",
                Status = request.IsActive ? "Active" : "Inactive"
            });
        }

        return NotFound(new { error = $"Subscription plan {id} not found." });
    }

    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        return await TogglePlanStatus(id);
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        return await ArchivePlan(id);
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        return await RestorePlan(id);
    }

    // ═══════════════════════════════════════════════════════
    // 3. CANONICAL MODULE MANAGEMENT & PRICING
    // ═══════════════════════════════════════════════════════

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules()
    {
        var modules = await _db.AppModules
            .AsNoTracking()
            .OrderBy(m => m.ModuleId)
            .Select(m => new AppModuleDto
            {
                ModuleId = m.ModuleId,
                ModuleCode = m.ModuleCode,
                ModuleName = m.ModuleName,
                Description = m.Description,
                PricePerMonth = m.PricePerMonth,
                IsActive = m.IsActive
            })
            .ToListAsync();

        return Ok(modules);
    }

    [HttpPut("modules/{moduleId:int}/price")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> UpdateModulePrice(int moduleId, [FromBody] UpdateModulePriceRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var module = await _db.AppModules.FirstOrDefaultAsync(m => m.ModuleId == moduleId);
        if (module is null) return NotFound(new { error = $"Module {moduleId} not found." });

        decimal oldPrice = module.PricePerMonth;
        module.PricePerMonth = request.PricePerMonth;
        module.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("UpdatePrice", "AppModule", module.ModuleId.ToString(),
            $"Updated module '{module.ModuleName}' monthly price from ₱{oldPrice:N2} to ₱{module.PricePerMonth:N2}.");

        return Ok(new
        {
            message = $"Module '{module.ModuleName}' price updated to ₱{module.PricePerMonth:N2}/month.",
            moduleId = module.ModuleId,
            moduleCode = module.ModuleCode,
            newPrice = module.PricePerMonth
        });
    }

    // ═══════════════════════════════════════════════════════
    // 4. COMPANY SUBSCRIPTIONS, UPGRADES/DOWNGRADES & ADD-ONS
    // ═══════════════════════════════════════════════════════

    [HttpGet("companies")]
    public async Task<IActionResult> GetAllCompanySubscriptions()
    {
        var allModules = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();

        var companies = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .AsNoTracking()
            .OrderBy(c => c.CompanyId)
            .ToListAsync();

        var dtos = companies.Select(c => BuildCompanySubscriptionDto(c, allModules)).ToList();
        return Ok(dtos);
    }

    [HttpGet("companies/{companyId:int}")]
    public async Task<IActionResult> GetCompanySubscription(int companyId)
    {
        var allModules = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();

        var company = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company is null) return NotFound(new { error = $"Company ID {companyId} not found." });

        return Ok(BuildCompanySubscriptionDto(company, allModules));
    }

    [HttpPost("companies/{companyId:int}/plan")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> ChangeCompanyPlan(int companyId, [FromBody] ChangeCompanyPlanRequest request)
    {
        var company = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company is null) return NotFound(new { error = $"Company ID {companyId} not found." });

        SubscriptionPlan? targetPlan = null;
        if (request.NewPlanId.HasValue && request.NewPlanId.Value > 0)
        {
            targetPlan = await _db.SubscriptionPlans
                .Include(p => p.PlanModules)
                    .ThenInclude(pm => pm.Module)
                .FirstOrDefaultAsync(p => p.SubscriptionPlanId == request.NewPlanId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.NewPlanCode))
        {
            var code = request.NewPlanCode.Trim().ToUpperInvariant();
            targetPlan = await _db.SubscriptionPlans
                .Include(p => p.PlanModules)
                    .ThenInclude(pm => pm.Module)
                .FirstOrDefaultAsync(p => p.PlanCode.ToUpper() == code);
        }

        if (targetPlan is null) return BadRequest(new { error = "Target subscription plan was not found or is invalid." });
        if (!targetPlan.IsActive || targetPlan.IsArchived) return BadRequest(new { error = $"Plan '{targetPlan.PlanName}' is currently inactive or archived." });

        var sub = company.Subscription;
        decimal prevPrice = 0m;
        string prevPlanName = "None";

        if (sub == null)
        {
            sub = new Subscription
            {
                CompanyId = company.CompanyId,
                SubscriptionPlanId = targetPlan.SubscriptionPlanId,
                SubscriptionName = targetPlan.PlanName,
                BillingCycle = targetPlan.BillingCycle ?? "Monthly",
                Duration = "Monthly",
                DurationMonths = 1,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                IsActive = true,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _db.Subscriptions.Add(sub);
            await _db.SaveChangesAsync();
            company.SubscriptionId = sub.SubscriptionId;
        }
        else
        {
            prevPrice = sub.PricePerMonth;
            prevPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName;
        }

        // Determine Upgrade vs Downgrade
        string changeType;
        if (targetPlan.PricePerMonth > prevPrice)
            changeType = "UPGRADE";
        else if (targetPlan.PricePerMonth < prevPrice && prevPrice > 0)
            changeType = "DOWNGRADE";
        else
            changeType = "PLAN_CHANGE";

        // Assign new plan
        sub.SubscriptionPlanId = targetPlan.SubscriptionPlanId;
        sub.SubscriptionName = targetPlan.PlanName;
        sub.Plan = targetPlan;

        // Recalculate price: Plan Price + Active Addons
        decimal activeAddonsTotal = sub.SubscriptionModules
            .Where(m => m.IsActive && m.IsAddon)
            .Sum(m => m.MonthlyPrice);

        sub.PricePerMonth = targetPlan.PricePerMonth + activeAddonsTotal;
        sub.UpdatedAt = DateTime.UtcNow;

        // Audit & History logging
        var history = new SubscriptionHistory
        {
            CompanyId = company.CompanyId,
            PreviousPlanName = prevPlanName,
            NewPlanName = targetPlan.PlanName,
            ChangeType = changeType,
            PreviousPrice = prevPrice,
            NewPrice = sub.PricePerMonth,
            Notes = request.Reason ?? $"{changeType} to plan '{targetPlan.PlanName}' (₱{sub.PricePerMonth:N2}/mo). Company data preserved.",
            EffectiveDate = DateTime.UtcNow,
            ChangedBy = User.Identity?.Name ?? "Super Admin",
            Timestamp = DateTime.UtcNow
        };
        _db.SubscriptionHistories.Add(history);

        await _db.SaveChangesAsync();

        await _audit.WriteAsync(changeType, "Subscription", sub.SubscriptionId.ToString(),
            $"{changeType} plan for company '{company.CompanyName}' from '{prevPlanName}' to '{targetPlan.PlanName}'. New Monthly Total: ₱{sub.PricePerMonth:N2}.");

        var allModules = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();
        return Ok(BuildCompanySubscriptionDto(company, allModules));
    }

    [HttpPost("companies/{companyId:int}/addons")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> AddModuleAddon(int companyId, [FromBody] AddModuleAddonRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var company = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company is null) return NotFound(new { error = $"Company ID {companyId} not found." });
        if (company.Subscription is null) return BadRequest(new { error = "Company does not have an active subscription to add add-ons to." });

        var code = request.ModuleCode.Trim().ToUpperInvariant();
        var module = await _db.AppModules.FirstOrDefaultAsync(m => m.ModuleCode.ToUpper() == code);
        if (module is null) return NotFound(new { error = $"Module '{code}' not found." });

        var sub = company.Subscription;
        decimal prevPrice = sub.PricePerMonth;

        var existingItem = sub.SubscriptionModules.FirstOrDefault(sm => sm.ModuleId == module.ModuleId);
        if (existingItem != null)
        {
            existingItem.IsActive = true;
            existingItem.IsAddon = true;
            existingItem.MonthlyPrice = module.PricePerMonth;
        }
        else
        {
            sub.SubscriptionModules.Add(new CompanySubscriptionModule
            {
                SubscriptionId = sub.SubscriptionId,
                ModuleId = module.ModuleId,
                MonthlyPrice = module.PricePerMonth,
                IsActive = true,
                IsAddon = true,
                SubscribedAt = DateTime.UtcNow
            });
        }

        // Recalculate price: Plan price + sum of active add-ons
        decimal planPrice = sub.Plan?.PricePerMonth ?? 0m;
        decimal addonTotal = sub.SubscriptionModules.Where(m => m.IsActive && m.IsAddon).Sum(m => m.MonthlyPrice);
        sub.PricePerMonth = planPrice + addonTotal;
        sub.UpdatedAt = DateTime.UtcNow;

        var history = new SubscriptionHistory
        {
            CompanyId = company.CompanyId,
            PreviousPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            NewPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            ChangeType = "ADDON_ADDED",
            PreviousPrice = prevPrice,
            NewPrice = sub.PricePerMonth,
            Notes = $"Added add-on module '{module.ModuleName}' (₱{module.PricePerMonth:N2}/mo).",
            EffectiveDate = DateTime.UtcNow,
            ChangedBy = User.Identity?.Name ?? "Super Admin",
            Timestamp = DateTime.UtcNow
        };
        _db.SubscriptionHistories.Add(history);

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("AddAddon", "Subscription", sub.SubscriptionId.ToString(),
            $"Added add-on '{module.ModuleName}' to company '{company.CompanyName}'. New Monthly Total: ₱{sub.PricePerMonth:N2}.");

        var allModules = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();
        return Ok(BuildCompanySubscriptionDto(company, allModules));
    }

    [HttpDelete("companies/{companyId:int}/addons/{moduleCode}")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> RemoveModuleAddon(int companyId, string moduleCode)
    {
        var company = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company?.Subscription is null) return NotFound(new { error = $"Company ID {companyId} subscription not found." });

        var code = moduleCode.Trim().ToUpperInvariant();
        var module = await _db.AppModules.FirstOrDefaultAsync(m => m.ModuleCode.ToUpper() == code);
        if (module is null) return NotFound(new { error = $"Module '{code}' not found." });

        var sub = company.Subscription;
        var item = sub.SubscriptionModules.FirstOrDefault(sm => sm.ModuleId == module.ModuleId && sm.IsAddon);
        if (item is null) return NotFound(new { error = $"Add-on module '{code}' is not active for company {companyId}." });

        decimal prevPrice = sub.PricePerMonth;
        _db.CompanySubscriptionModules.Remove(item);

        // Recalculate price
        decimal planPrice = sub.Plan?.PricePerMonth ?? 0m;
        decimal addonTotal = sub.SubscriptionModules.Where(m => m != item && m.IsActive && m.IsAddon).Sum(m => m.MonthlyPrice);
        sub.PricePerMonth = planPrice + addonTotal;
        sub.UpdatedAt = DateTime.UtcNow;

        var history = new SubscriptionHistory
        {
            CompanyId = company.CompanyId,
            PreviousPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            NewPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            ChangeType = "ADDON_REMOVED",
            PreviousPrice = prevPrice,
            NewPrice = sub.PricePerMonth,
            Notes = $"Removed add-on module '{module.ModuleName}'.",
            EffectiveDate = DateTime.UtcNow,
            ChangedBy = User.Identity?.Name ?? "Super Admin",
            Timestamp = DateTime.UtcNow
        };
        _db.SubscriptionHistories.Add(history);

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("RemoveAddon", "Subscription", sub.SubscriptionId.ToString(),
            $"Removed add-on '{module.ModuleName}' from company '{company.CompanyName}'. New Monthly Total: ₱{sub.PricePerMonth:N2}.");

        var allModules = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();
        return Ok(BuildCompanySubscriptionDto(company, allModules));
    }

    [HttpPost("companies/{companyId:int}/modules")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> AddModuleToCompany(int companyId, [FromBody] AddModuleToCompanyRequest request)
    {
        return await AddModuleAddon(companyId, new AddModuleAddonRequest { ModuleCode = request.ModuleCode });
    }

    [HttpDelete("companies/{companyId:int}/modules/{moduleId:int}")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> RemoveModuleFromCompany(int companyId, int moduleId)
    {
        var mod = await _db.AppModules.FirstOrDefaultAsync(m => m.ModuleId == moduleId);
        if (mod == null) return NotFound(new { error = $"Module {moduleId} not found." });
        return await RemoveModuleAddon(companyId, mod.ModuleCode);
    }

    [HttpDelete("companies/{companyId:int}/modules/by-code/{moduleCode}")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> RemoveModuleByCodeFromCompany(int companyId, string moduleCode)
    {
        return await RemoveModuleAddon(companyId, moduleCode);
    }

    [HttpPatch("companies/{companyId:int}/toggle-status")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> ToggleCompanySubscriptionStatus(int companyId)
    {
        var company = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company?.Subscription is null) return NotFound(new { error = $"Company ID {companyId} has no subscription configured." });

        var sub = company.Subscription;
        sub.IsActive = !sub.IsActive;
        sub.Status = sub.IsActive ? "Active" : "Inactive";
        sub.UpdatedAt = DateTime.UtcNow;

        string action = sub.IsActive ? "Activated" : "Deactivated";
        var history = new SubscriptionHistory
        {
            CompanyId = company.CompanyId,
            PreviousPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            NewPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            ChangeType = "STATUS_CHANGE",
            PreviousPrice = sub.PricePerMonth,
            NewPrice = sub.PricePerMonth,
            Notes = $"Subscription status changed to {sub.Status}.",
            EffectiveDate = DateTime.UtcNow,
            ChangedBy = User.Identity?.Name ?? "Super Admin",
            Timestamp = DateTime.UtcNow
        };
        _db.SubscriptionHistories.Add(history);

        await _db.SaveChangesAsync();

        await _audit.WriteAsync(action, "Subscription", sub.SubscriptionId.ToString(),
            $"{action} subscription for '{company.CompanyName}'.");

        var allModules = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();
        return Ok(BuildCompanySubscriptionDto(company, allModules));
    }

    [HttpPut("companies/{companyId:int}/subscription")]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> UpdateCompanySubscription(int companyId, [FromBody] UpdateCompanySubscriptionRequest request)
    {
        var company = await _db.Companies
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.Plan)
                    .ThenInclude(p => p!.PlanModules)
                        .ThenInclude(pm => pm.Module)
            .Include(c => c.Subscription)
                .ThenInclude(s => s!.SubscriptionModules)
                    .ThenInclude(sm => sm.Module)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (company is null) return NotFound(new { error = $"Company ID {companyId} not found." });

        var sub = company.Subscription;
        if (sub == null)
        {
            sub = new Subscription
            {
                CompanyId = company.CompanyId,
                SubscriptionName = $"{company.CompanyName} Subscription",
                BillingCycle = request.BillingCycle ?? "Monthly",
                Duration = "Monthly",
                DurationMonths = 1,
                StartDate = request.StartDate ?? DateTime.UtcNow,
                EndDate = request.EndDate ?? DateTime.UtcNow.AddYears(1),
                IsActive = request.IsActive ?? true,
                Status = request.Status ?? "Active",
                CreatedAt = DateTime.UtcNow
            };
            _db.Subscriptions.Add(sub);
            await _db.SaveChangesAsync();
            company.SubscriptionId = sub.SubscriptionId;
        }

        decimal prevPrice = sub.PricePerMonth;
        string prevPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName;

        // If plan specified, update plan
        if (request.PlanId.HasValue && request.PlanId.Value > 0)
        {
            var plan = await _db.SubscriptionPlans.Include(p => p.PlanModules).FirstOrDefaultAsync(p => p.SubscriptionPlanId == request.PlanId.Value);
            if (plan != null)
            {
                sub.SubscriptionPlanId = plan.SubscriptionPlanId;
                sub.SubscriptionName = plan.PlanName;
                sub.Plan = plan;
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.PlanCode))
        {
            var code = request.PlanCode.Trim().ToUpperInvariant();
            var plan = await _db.SubscriptionPlans.Include(p => p.PlanModules).FirstOrDefaultAsync(p => p.PlanCode.ToUpper() == code);
            if (plan != null)
            {
                sub.SubscriptionPlanId = plan.SubscriptionPlanId;
                sub.SubscriptionName = plan.PlanName;
                sub.Plan = plan;
            }
        }

        if (request.IsActive.HasValue)
        {
            sub.IsActive = request.IsActive.Value;
            sub.Status = sub.IsActive ? "Active" : "Inactive";
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
            sub.Status = request.Status.Trim();
        if (request.StartDate.HasValue)
            sub.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue)
            sub.EndDate = request.EndDate.Value;
        if (!string.IsNullOrWhiteSpace(request.BillingCycle))
            sub.BillingCycle = request.BillingCycle.Trim();

        var allAppModules = await _db.AppModules.ToListAsync();

        // Process AddonCodes or ModuleCodes
        var desiredAddonCodes = request.AddonCodes ?? request.ModuleCodes;
        if (desiredAddonCodes != null)
        {
            var requestedUpper = desiredAddonCodes.Select(c => c.Trim().ToUpperInvariant()).ToHashSet();

            // Retain/remove existing
            foreach (var sm in sub.SubscriptionModules.ToList())
            {
                var mod = allAppModules.FirstOrDefault(m => m.ModuleId == sm.ModuleId);
                if (mod != null)
                {
                    if (requestedUpper.Contains(mod.ModuleCode.ToUpperInvariant()))
                    {
                        sm.IsActive = true;
                        sm.IsAddon = true;
                        sm.MonthlyPrice = mod.PricePerMonth;
                    }
                    else
                    {
                        _db.CompanySubscriptionModules.Remove(sm);
                    }
                }
            }

            // Add new
            foreach (var code in requestedUpper)
            {
                var mod = allAppModules.FirstOrDefault(m => m.ModuleCode.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (mod != null && !sub.SubscriptionModules.Any(sm => sm.ModuleId == mod.ModuleId))
                {
                    sub.SubscriptionModules.Add(new CompanySubscriptionModule
                    {
                        SubscriptionId = sub.SubscriptionId,
                        ModuleId = mod.ModuleId,
                        MonthlyPrice = mod.PricePerMonth,
                        IsActive = true,
                        IsAddon = true,
                        SubscribedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Recalculate price: Plan Price + Active Addons
        decimal planPrice = sub.Plan?.PricePerMonth ?? 0m;
        decimal addonTotal = sub.SubscriptionModules.Where(m => m.IsActive && m.IsAddon).Sum(m => m.MonthlyPrice);
        sub.PricePerMonth = sub.Plan != null ? (planPrice + addonTotal) : sub.SubscriptionModules.Where(m => m.IsActive).Sum(m => m.MonthlyPrice);
        sub.UpdatedAt = DateTime.UtcNow;

        var history = new SubscriptionHistory
        {
            CompanyId = company.CompanyId,
            PreviousPlanName = prevPlanName,
            NewPlanName = sub.Plan?.PlanName ?? sub.SubscriptionName,
            ChangeType = sub.Plan?.PlanName != prevPlanName ? "PLAN_CHANGE" : "UPDATE",
            PreviousPrice = prevPrice,
            NewPrice = sub.PricePerMonth,
            Notes = $"Updated subscription configuration for '{company.CompanyName}'.",
            EffectiveDate = DateTime.UtcNow,
            ChangedBy = User.Identity?.Name ?? "Super Admin",
            Timestamp = DateTime.UtcNow
        };
        _db.SubscriptionHistories.Add(history);

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("UpdateSubscription", "Subscription", sub.SubscriptionId.ToString(),
            $"Updated subscription for '{company.CompanyName}'. Total: ₱{sub.PricePerMonth:N2}/mo, Status: {sub.Status}.");

        var allModulesList = await _db.AppModules.AsNoTracking().OrderBy(m => m.ModuleId).ToListAsync();
        return Ok(BuildCompanySubscriptionDto(company, allModulesList));
    }

    // ═══════════════════════════════════════════════════════
    // 5. AUDIT HISTORY ENDPOINTS
    // ═══════════════════════════════════════════════════════

    [HttpGet("history")]
    public async Task<IActionResult> GetAllHistory()
    {
        var history = await _db.SubscriptionHistories
            .Include(h => h.Company)
            .AsNoTracking()
            .OrderByDescending(h => h.Timestamp)
            .Take(100)
            .Select(h => new SubscriptionHistoryDto
            {
                SubscriptionHistoryId = h.SubscriptionHistoryId,
                CompanyId = h.CompanyId,
                CompanyName = h.Company != null ? h.Company.CompanyName : $"Company #{h.CompanyId}",
                PreviousPlanName = h.PreviousPlanName,
                NewPlanName = h.NewPlanName,
                ChangeType = h.ChangeType,
                PreviousPrice = h.PreviousPrice,
                NewPrice = h.NewPrice,
                Notes = h.Notes,
                EffectiveDate = h.EffectiveDate,
                ChangedBy = h.ChangedBy,
                Timestamp = h.Timestamp
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpGet("companies/{companyId:int}/history")]
    public async Task<IActionResult> GetCompanyHistory(int companyId)
    {
        var history = await _db.SubscriptionHistories
            .Include(h => h.Company)
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new SubscriptionHistoryDto
            {
                SubscriptionHistoryId = h.SubscriptionHistoryId,
                CompanyId = h.CompanyId,
                CompanyName = h.Company != null ? h.Company.CompanyName : $"Company #{h.CompanyId}",
                PreviousPlanName = h.PreviousPlanName,
                NewPlanName = h.NewPlanName,
                ChangeType = h.ChangeType,
                PreviousPrice = h.PreviousPrice,
                NewPrice = h.NewPrice,
                Notes = h.Notes,
                EffectiveDate = h.EffectiveDate,
                ChangedBy = h.ChangedBy,
                Timestamp = h.Timestamp
            })
            .ToListAsync();

        return Ok(history);
    }

    // ═══════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════

    private static SubscriptionPlanDto MapToPlanDto(SubscriptionPlan plan, int subscribedCompaniesCount)
    {
        var included = plan.PlanModules?
            .Where(pm => pm.Module != null)
            .Select(pm => new AppModuleDto
            {
                ModuleId = pm.Module!.ModuleId,
                ModuleCode = pm.Module.ModuleCode,
                ModuleName = pm.Module.ModuleName,
                Description = pm.Module.Description,
                PricePerMonth = pm.Module.PricePerMonth,
                IsActive = pm.Module.IsActive
            })
            .ToList() ?? new List<AppModuleDto>();

        return new SubscriptionPlanDto
        {
            PlanId = plan.SubscriptionPlanId,
            PlanCode = plan.PlanCode,
            PlanName = plan.PlanName,
            Description = plan.Description,
            Price = plan.PricePerMonth,
            BillingInterval = plan.BillingCycle ?? "Monthly",
            Status = plan.Status ?? (plan.IsActive ? "Active" : "Inactive"),
            IsActive = plan.IsActive,
            IsArchived = plan.IsArchived,
            MaxUsers = plan.MaxUsers,
            MaxBranches = plan.MaxBranches,
            MaxDevices = plan.MaxDevices,
            CreatedAt = plan.CreatedAt,
            IncludedModules = included,
            SubscribedCompaniesCount = subscribedCompaniesCount
        };
    }

    private static CompanySubscriptionDetailDto BuildCompanySubscriptionDto(Company company, List<AppModule> allModules)
    {
        var sub = company.Subscription;
        var plan = sub?.Plan;

        var planModulesList = new List<AppModuleDto>();
        if (plan?.PlanModules != null)
        {
            foreach (var pm in plan.PlanModules)
            {
                var mod = pm.Module ?? allModules.FirstOrDefault(m => m.ModuleId == pm.ModuleId);
                if (mod != null && mod.IsActive)
                {
                    planModulesList.Add(new AppModuleDto
                    {
                        ModuleId = mod.ModuleId,
                        ModuleCode = mod.ModuleCode,
                        ModuleName = mod.ModuleName,
                        Description = mod.Description,
                        PricePerMonth = mod.PricePerMonth,
                        IsActive = mod.IsActive
                    });
                }
            }
        }

        var activeAddonsList = new List<CompanySubscribedModuleDto>();
        if (sub?.SubscriptionModules != null)
        {
            foreach (var sm in sub.SubscriptionModules.Where(x => x.IsActive && x.IsAddon))
            {
                var mod = sm.Module ?? allModules.FirstOrDefault(m => m.ModuleId == sm.ModuleId);
                activeAddonsList.Add(new CompanySubscribedModuleDto
                {
                    CompanySubscriptionModuleId = sm.CompanySubscriptionModuleId,
                    ModuleId = sm.ModuleId,
                    ModuleCode = mod?.ModuleCode ?? string.Empty,
                    ModuleName = mod?.ModuleName ?? $"Module {sm.ModuleId}",
                    Description = mod?.Description,
                    MonthlyPrice = sm.MonthlyPrice > 0 ? sm.MonthlyPrice : (mod?.PricePerMonth ?? 0m),
                    IsActive = sm.IsActive,
                    IsAddon = sm.IsAddon,
                    SubscribedAt = sm.SubscribedAt
                });
            }
        }

        // Effective modules = Union of Plan Modules + Active Addons
        var effectiveSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pm in planModulesList)
        {
            if (!string.IsNullOrWhiteSpace(pm.ModuleCode)) effectiveSet.Add(pm.ModuleCode.ToUpperInvariant());
        }
        foreach (var addon in activeAddonsList)
        {
            if (!string.IsNullOrWhiteSpace(addon.ModuleCode)) effectiveSet.Add(addon.ModuleCode.ToUpperInvariant());
        }

        // Backwards compatibility list
        var subscribedList = new List<CompanySubscribedModuleDto>();
        if (sub?.SubscriptionModules != null)
        {
            foreach (var sm in sub.SubscriptionModules.Where(x => x.IsActive))
            {
                var mod = sm.Module ?? allModules.FirstOrDefault(m => m.ModuleId == sm.ModuleId);
                subscribedList.Add(new CompanySubscribedModuleDto
                {
                    CompanySubscriptionModuleId = sm.CompanySubscriptionModuleId,
                    ModuleId = sm.ModuleId,
                    ModuleCode = mod?.ModuleCode ?? string.Empty,
                    ModuleName = mod?.ModuleName ?? $"Module {sm.ModuleId}",
                    Description = mod?.Description,
                    MonthlyPrice = sm.MonthlyPrice,
                    IsActive = sm.IsActive,
                    IsAddon = sm.IsAddon,
                    SubscribedAt = sm.SubscribedAt
                });
                if (mod != null) effectiveSet.Add(mod.ModuleCode.ToUpperInvariant());
            }
        }

        decimal planPrice = plan?.PricePerMonth ?? 0m;
        decimal addonTotal = activeAddonsList.Sum(a => a.MonthlyPrice);
        decimal monthlyTotal = plan != null ? (planPrice + addonTotal) : (sub?.PricePerMonth ?? subscribedList.Sum(s => s.MonthlyPrice));

        return new CompanySubscriptionDetailDto
        {
            CompanyId = company.CompanyId,
            CompanyCode = company.CompanyCode,
            CompanyName = company.CompanyName,
            SubscriptionId = sub?.SubscriptionId,
            PlanId = plan?.SubscriptionPlanId,
            PlanCode = plan?.PlanCode ?? string.Empty,
            PlanName = plan?.PlanName ?? sub?.SubscriptionName ?? "No Plan",
            PlanPrice = planPrice,
            SubscriptionName = plan?.PlanName ?? sub?.SubscriptionName ?? "No Active Subscription",
            Status = sub?.Status ?? (company.IsActive ? "Active" : "Inactive"),
            IsActive = sub?.IsActive ?? false,
            StartDate = sub?.StartDate ?? DateTime.UtcNow,
            EndDate = sub?.EndDate ?? DateTime.UtcNow.AddMonths(1),
            BillingCycle = sub?.BillingCycle ?? "Monthly",
            PlanModules = planModulesList,
            ActiveAddons = activeAddonsList,
            EffectiveModules = effectiveSet.ToList(),
            AddonTotal = addonTotal,
            MonthlyTotal = monthlyTotal,
            SubscribedModules = subscribedList,
            AvailableModules = allModules.Select(m => new AppModuleDto
            {
                ModuleId = m.ModuleId,
                ModuleCode = m.ModuleCode,
                ModuleName = m.ModuleName,
                Description = m.Description,
                PricePerMonth = m.PricePerMonth,
                IsActive = m.IsActive
            }).ToList()
        };
    }
}