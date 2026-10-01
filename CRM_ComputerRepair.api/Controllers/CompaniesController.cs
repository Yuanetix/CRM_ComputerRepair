using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("companies")]
[Authorize(Roles = "Admin,Super Admin")]
public class CompaniesController : ControllerBase
{
    private readonly MasterCrmDbContext _db;
    private readonly UserManager<User> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ITenantDbContextFactory _tenantFactory;
    private readonly IAuditWriter _audit;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(
        MasterCrmDbContext db,
        UserManager<User> users,
        RoleManager<IdentityRole> roles,
        ITenantDbContextFactory tenantFactory,
        IAuditWriter audit,
        ILogger<CompaniesController> logger)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _tenantFactory = tenantFactory;
        _audit = audit;
        _logger = logger;
    }

    private static volatile bool _masterBranchSchemaEnsured = false;

    private async Task EnsureMasterBranchColumnsAsync()
    {
        if (_masterBranchSchemaEnsured) return;
        try
        {
            await _db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('Devices', 'U') IS NOT NULL AND COL_LENGTH('Devices', 'BranchId') IS NULL ALTER TABLE Devices ADD BranchId INT NULL;
IF OBJECT_ID('Customers', 'U') IS NOT NULL AND COL_LENGTH('Customers', 'BranchId') IS NULL ALTER TABLE Customers ADD BranchId INT NULL;
IF OBJECT_ID('RepairRequests', 'U') IS NOT NULL AND COL_LENGTH('RepairRequests', 'BranchId') IS NULL ALTER TABLE RepairRequests ADD BranchId INT NULL;
IF OBJECT_ID('CustomerInteractions', 'U') IS NOT NULL AND COL_LENGTH('CustomerInteractions', 'BranchId') IS NULL ALTER TABLE CustomerInteractions ADD BranchId INT NULL;
IF OBJECT_ID('FollowUps', 'U') IS NOT NULL AND COL_LENGTH('FollowUps', 'BranchId') IS NULL ALTER TABLE FollowUps ADD BranchId INT NULL;
IF OBJECT_ID('Payments', 'U') IS NOT NULL AND COL_LENGTH('Payments', 'BranchId') IS NULL ALTER TABLE Payments ADD BranchId INT NULL;
IF OBJECT_ID('AspNetUsers', 'U') IS NOT NULL AND COL_LENGTH('AspNetUsers', 'BranchId') IS NULL ALTER TABLE AspNetUsers ADD BranchId INT NULL;
IF OBJECT_ID('AspNetUsers', 'U') IS NOT NULL AND COL_LENGTH('AspNetUsers', 'AssignedBranchName') IS NULL ALTER TABLE AspNetUsers ADD AssignedBranchName NVARCHAR(200) NULL;
IF OBJECT_ID('AspNetUsers', 'U') IS NOT NULL UPDATE AspNetUsers SET CompanyId = NULL WHERE UserName = 'superadmin';
");
            _masterBranchSchemaEnsured = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify/add BranchId on Master database tables.");
        }
    }

    private sealed record CompanyUserItem
    {
        public string Id { get; init; } = string.Empty;
        public int? CompanyId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public string? UserName { get; init; }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await EnsureMasterBranchColumnsAsync();

        var companies = await _db.Companies
            .Include(c => c.Subscription)
            .Include(c => c.CompanyDatabases)
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        List<CompanyUserItem> allUsers;
        try
        {
            allUsers = await _users.Users.AsNoTracking()
                .Where(u => u.CompanyId != null && u.UserName != "superadmin")
                .Select(u => new CompanyUserItem
                {
                    Id = u.Id,
                    CompanyId = u.CompanyId,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    UserName = u.UserName
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load users for company overview. Proceeding with empty user list.");
            allUsers = new List<CompanyUserItem>();
        }

        Dictionary<int, int> deviceCounts = new();
        try
        {
            deviceCounts = await _db.Devices.AsNoTracking()
                .Select(d => d.CompanyId)
                .GroupBy(cid => cid)
                .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CompanyId, x => x.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load device counts by company.");
        }

        var result = companies.Select(c =>
        {
            var dbRow = c.CompanyDatabases.FirstOrDefault(d => d.IsActive) ?? c.CompanyDatabases.FirstOrDefault();
            var adminUser = allUsers.FirstOrDefault(u => u.CompanyId == c.CompanyId && (u.UserName == c.CompanyCode.ToLowerInvariant() || (u.UserName != null && u.UserName.EndsWith("admin", StringComparison.OrdinalIgnoreCase))))
                         ?? allUsers.FirstOrDefault(u => u.CompanyId == c.CompanyId);

            return new CompanyDetailDto
            {
                CompanyId = c.CompanyId,
                CompanyCode = c.CompanyCode,
                CompanyName = c.CompanyName,
                ContactPhone = c.ContactPhone,
                ContactEmail = c.ContactEmail,
                Address = c.Address,
                City = c.City,
                StateOrProvince = c.StateOrProvince,
                PostalCode = c.PostalCode,
                Country = c.Country ?? "Philippines",
                IsActive = c.IsActive,
                HasAcceptedTerms = c.HasAcceptedTerms,
                TermsAcceptedAt = c.TermsAcceptedAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,

                DatabaseServer = dbRow?.ServerName ?? @"(localdb)\MSSQLLocalDB",
                DatabaseName = dbRow?.DatabaseName ?? $"DB_TenantRepairs_Company{c.CompanyId}",
                CredentialKey = dbRow?.CredentialKey,

                SubscriptionId = c.SubscriptionId,
                SubscriptionPlanName = c.Subscription?.SubscriptionName,
                SubscriptionPrice = c.Subscription?.PricePerMonth,
                SubscriptionDuration = c.Subscription?.Duration ?? (c.Subscription != null ? $"{c.Subscription.DurationMonths} Month(s)" : null),
                MaxUsers = c.Subscription?.MaxUsers,
                MaxDevices = c.Subscription?.MaxDevices,
                EnableMultiBranching = c.Subscription?.EnableMultiBranching ?? false,

                AdminUserId = adminUser?.Id,
                AdminFullName = adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
                AdminEmail = adminUser?.Email,
                AdminUsername = adminUser?.UserName,

                TotalUsersCount = allUsers.Count(u => u.CompanyId == c.CompanyId),
                TotalDevicesCount = deviceCounts.TryGetValue(c.CompanyId, out int devCount) ? devCount : 0
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var companies = await _db.Companies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .Select(c => new
            {
                c.CompanyId,
                c.CompanyCode,
                c.CompanyName
            })
            .ToListAsync();

        return Ok(companies);
    }

    [HttpGet("generate-code")]
    public async Task<IActionResult> GenerateCode()
    {
        string code;
        int attempts = 0;
        do
        {
            var year = DateTime.UtcNow.Year;
            var randPart = RandomNumberGenerator.GetInt32(1000, 9999);
            code = $"CMP-{year}-{randPart}";
            attempts++;
        } while (await _db.Companies.AnyAsync(c => c.CompanyCode == code) && attempts < 20);

        return Ok(new { companyCode = code });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        await EnsureMasterBranchColumnsAsync();

        var c = await _db.Companies
            .Include(c => c.Subscription)
            .Include(c => c.CompanyDatabases)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == id);

        if (c is null) return NotFound(new { error = $"Company ID {id} not found." });

        List<CompanyUserItem> allUsers;
        try
        {
            allUsers = await _users.Users.AsNoTracking()
                .Where(u => u.CompanyId == id && u.UserName != "superadmin")
                .Select(u => new CompanyUserItem
                {
                    Id = u.Id,
                    CompanyId = u.CompanyId,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    UserName = u.UserName
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load users for Company ID {CompanyId}.", id);
            allUsers = new List<CompanyUserItem>();
        }

        int totalDevices = 0;
        try
        {
            totalDevices = await _db.Devices.AsNoTracking().CountAsync(d => d.CompanyId == id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count devices for Company ID {CompanyId}.", id);
        }

        var dbRow = c.CompanyDatabases.FirstOrDefault(d => d.IsActive) ?? c.CompanyDatabases.FirstOrDefault();
        var adminUser = allUsers.FirstOrDefault(u => u.UserName == c.CompanyCode.ToLowerInvariant() || (u.UserName != null && u.UserName.EndsWith("admin", StringComparison.OrdinalIgnoreCase)))
                     ?? allUsers.FirstOrDefault();

        var dto = new CompanyDetailDto
        {
            CompanyId = c.CompanyId,
            CompanyCode = c.CompanyCode,
            CompanyName = c.CompanyName,
            ContactPhone = c.ContactPhone,
            ContactEmail = c.ContactEmail,
            Address = c.Address,
            City = c.City,
            StateOrProvince = c.StateOrProvince,
            PostalCode = c.PostalCode,
            Country = c.Country ?? "Philippines",
            IsActive = c.IsActive,
            HasAcceptedTerms = c.HasAcceptedTerms,
            TermsAcceptedAt = c.TermsAcceptedAt,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,

            DatabaseServer = dbRow?.ServerName ?? @"(localdb)\MSSQLLocalDB",
            DatabaseName = dbRow?.DatabaseName ?? $"DB_TenantRepairs_Company{c.CompanyId}",
            CredentialKey = dbRow?.CredentialKey,

            SubscriptionId = c.SubscriptionId,
            SubscriptionPlanName = c.Subscription?.SubscriptionName,
            SubscriptionPrice = c.Subscription?.PricePerMonth,
            SubscriptionDuration = c.Subscription?.Duration ?? (c.Subscription != null ? $"{c.Subscription.DurationMonths} Month(s)" : null),
            MaxUsers = c.Subscription?.MaxUsers,
            MaxDevices = c.Subscription?.MaxDevices,
            EnableMultiBranching = c.Subscription?.EnableMultiBranching ?? false,

            AdminUserId = adminUser?.Id,
            AdminFullName = adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}".Trim() : null,
            AdminEmail = adminUser?.Email,
            AdminUsername = adminUser?.UserName,

            TotalUsersCount = allUsers.Count,
            TotalDevicesCount = totalDevices
        };

        return Ok(dto);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCompanyRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var code = request.CompanyCode.Trim().ToUpperInvariant();
        if (await _db.Companies.AnyAsync(c => c.CompanyCode == code))
        {
            return BadRequest(new { error = $"Company Code '{code}' is already registered. Please choose or generate another code." });
        }

        var username = request.AdminUsername.Trim();
        if (await _users.FindByNameAsync(username) != null)
        {
            return BadRequest(new { error = $"Username '{username}' is already taken." });
        }

        var email = request.AdminEmail.Trim();
        if (await _users.FindByEmailAsync(email) != null)
        {
            return BadRequest(new { error = $"Email '{email}' is already registered." });
        }

        // 1. Create company entity
        var company = new Company
        {
            CompanyCode = code,
            CompanyName = request.CompanyName.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            StateOrProvince = request.StateOrProvince?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "Philippines" : request.Country.Trim(),
            SubscriptionId = request.SubscriptionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        // 2. Multi-tenant database mapping
        var serverName = string.IsNullOrWhiteSpace(request.DatabaseServer)
            ? @"(localdb)\MSSQLLocalDB"
            : request.DatabaseServer.Trim();

        var dbName = string.IsNullOrWhiteSpace(request.DatabaseName)
            ? $"DB_TenantRepairs_{company.CompanyCode.Replace("-", "_")}"
            : request.DatabaseName.Trim();

        var compDb = new CompanyDatabase
        {
            CompanyId = company.CompanyId,
            ServerName = serverName,
            DatabaseName = dbName,
            CredentialKey = request.CredentialKey?.Trim() ?? string.Empty,
            IsActive = true
        };

        _db.CompanyDatabases.Add(compDb);
        await _db.SaveChangesAsync();

        // 3. Create initial business admin account
        if (!await _roles.RoleExistsAsync("Admin"))
            await _roles.CreateAsync(new IdentityRole("Admin"));

        var adminUser = new User
        {
            UserName = username,
            Email = email,
            FirstName = request.AdminFirstName.Trim(),
            LastName = request.AdminLastName.Trim(),
            CompanyId = company.CompanyId,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var userResult = await _users.CreateAsync(adminUser, request.AdminPassword);
        if (!userResult.Succeeded)
        {
            var errors = string.Join("; ", userResult.Errors.Select(e => e.Description));
            return BadRequest(new { error = $"Failed to create admin user: {errors}" });
        }

        await _users.AddToRoleAsync(adminUser, "Admin");

        // 4. Provision tenant database schema and seed retention defaults
        try
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(company.CompanyId);
            await tenantDb.Database.MigrateAsync();

            if (!await tenantDb.RetentionEmailTemplates.AnyAsync())
            {
                tenantDb.RetentionEmailTemplates.AddRange(new[]
                {
                    new RetentionEmailTemplate
                    {
                        TemplateName = "We Miss You - 10% Off",
                        Subject = "Special Discount: 10% Off Your Next Computer Repair at {{CompanyName}}!",
                        Body = "Hi {{CustomerName}},\n\nIt's been a while since your last visit. We'd love to welcome you back with a 10% discount on any repair or diagnostic service!\n\nPromo Code: {{PromoCode}}\nExpires: {{ExpirationDate}}\n\nBest regards,\n{{CompanyName}} Team",
                        DefaultDiscountPercent = 10m
                    },
                    new RetentionEmailTemplate
                    {
                        TemplateName = "VIP Device Tune-up - 15% Off",
                        Subject = "Exclusive Offer: 15% Off Diagnostic & Cleaning Service",
                        Body = "Dear {{CustomerName}},\n\nKeep your computer running at peak speed! Bring your laptop or desktop in for a complete tune-up and get 15% off.\n\nPromo Code: {{PromoCode}}\nExpires: {{ExpirationDate}}\n\nSincerely,\n{{CompanyName}}",
                        DefaultDiscountPercent = 15m
                    }
                });

                tenantDb.RetentionSettings.Add(new RetentionSettings
                {
                    SmtpHost = "localhost",
                    SmtpPort = 25,
                    SmtpFromEmail = string.IsNullOrWhiteSpace(company.ContactEmail) ? "repairs@fixorycrm.local" : company.ContactEmail,
                    SmtpFromName = company.CompanyName,
                    SmtpEnableSsl = false
                });

                await tenantDb.SaveChangesAsync();
            }

            // Provision company-specific starter loyalty programs in Master DB
            if (!await _db.LoyaltyPrograms.AnyAsync(p => p.CompanyId == company.CompanyId))
            {
                _db.LoyaltyPrograms.AddRange(new[]
                {
                    new LoyaltyProgram
                    {
                        CompanyId = company.CompanyId,
                        ProgramName = $"{company.CompanyName} Rewards Club",
                        Description = $"Earn 1 point per ₱1 spent. Receive 10% off after 3 completed repairs and ₱1,500 total spending.",
                        PointsPerPeso = 1,
                        DiscountPercentage = 10,
                        MinimumSpend = 500,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddYears(1),
                        PointsValidityDays = 365,
                        RedeemPointsRequired = 500,
                        MinTransactions = 3,
                        MinTotalSpent = 1500,
                        MaxInactiveDays = 120,
                        RewardType = LoyaltyRewardType.DiscountPercent,
                        RewardValue = 10,
                        MaxRedemptionsPerCustomer = 4,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new LoyaltyProgram
                    {
                        CompanyId = company.CompanyId,
                        ProgramName = $"{company.CompanyName} VIP Care Tier",
                        Description = $"Exclusive tier for frequent clients (₱8,000+ spend). Entitled to a free annual maintenance diagnostic.",
                        PointsPerPeso = 2,
                        DiscountPercentage = 0,
                        MinimumSpend = 8000,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddYears(1),
                        PointsValidityDays = 365,
                        RedeemPointsRequired = 1500,
                        MinTransactions = 5,
                        MinTotalSpent = 8000,
                        MaxInactiveDays = 365,
                        RewardType = LoyaltyRewardType.FreeService,
                        RewardValue = 1200,
                        MaxRedemptionsPerCustomer = 1,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                });
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tenant database provisioning encountered an exception for Company {CompanyId}; database will be initialized on first connection.", company.CompanyId);
        }

        // 5. Audit trail
        await _audit.WriteAsync("Register", "Company", company.CompanyId.ToString(),
            $"Registered business '{company.CompanyName}' ({company.CompanyCode}) with initial admin '{username}'.");

        return CreatedAtAction(nameof(GetById), new { id = company.CompanyId }, new CompanyDetailDto
        {
            CompanyId = company.CompanyId,
            CompanyCode = company.CompanyCode,
            CompanyName = company.CompanyName,
            ContactPhone = company.ContactPhone,
            ContactEmail = company.ContactEmail,
            Address = company.Address,
            City = company.City,
            StateOrProvince = company.StateOrProvince,
            PostalCode = company.PostalCode,
            Country = company.Country,
            IsActive = company.IsActive,
            CreatedAt = company.CreatedAt,
            DatabaseServer = compDb.ServerName,
            DatabaseName = compDb.DatabaseName,
            SubscriptionId = company.SubscriptionId,
            AdminUserId = adminUser.Id,
            AdminFullName = $"{adminUser.FirstName} {adminUser.LastName}".Trim(),
            AdminEmail = adminUser.Email,
            AdminUsername = adminUser.UserName,
            TotalUsersCount = 1,
            TotalDevicesCount = 0
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCompanyRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var company = await _db.Companies
            .Include(c => c.CompanyDatabases)
            .FirstOrDefaultAsync(c => c.CompanyId == id);

        if (company is null) return NotFound(new { error = $"Company ID {id} not found." });

        company.CompanyName = request.CompanyName.Trim();
        company.ContactPhone = request.ContactPhone?.Trim();
        company.ContactEmail = request.ContactEmail?.Trim();
        company.Address = request.Address?.Trim();
        company.City = request.City?.Trim();
        company.StateOrProvince = request.StateOrProvince?.Trim();
        company.PostalCode = request.PostalCode?.Trim();
        company.Country = string.IsNullOrWhiteSpace(request.Country) ? "Philippines" : request.Country.Trim();
        company.SubscriptionId = request.SubscriptionId;
        company.IsActive = request.IsActive;
        company.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.DatabaseServer) || !string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            var dbRow = company.CompanyDatabases.FirstOrDefault(d => d.IsActive) ?? company.CompanyDatabases.FirstOrDefault();
            if (dbRow != null)
            {
                if (!string.IsNullOrWhiteSpace(request.DatabaseServer)) dbRow.ServerName = request.DatabaseServer.Trim();
                if (!string.IsNullOrWhiteSpace(request.DatabaseName)) dbRow.DatabaseName = request.DatabaseName.Trim();
            }
            else
            {
                company.CompanyDatabases.Add(new CompanyDatabase
                {
                    CompanyId = company.CompanyId,
                    ServerName = string.IsNullOrWhiteSpace(request.DatabaseServer) ? @"(localdb)\MSSQLLocalDB" : request.DatabaseServer.Trim(),
                    DatabaseName = string.IsNullOrWhiteSpace(request.DatabaseName) ? $"DB_TenantRepairs_Company{company.CompanyId}" : request.DatabaseName.Trim(),
                    IsActive = true
                });
            }
        }

        await _db.SaveChangesAsync();

        await _audit.WriteAsync("Update", "Company", company.CompanyId.ToString(),
            $"Updated business details for '{company.CompanyName}'.");

        return await GetById(id);
    }

    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company is null) return NotFound(new { error = $"Company ID {id} not found." });

        company.IsActive = !company.IsActive;
        company.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        string action = company.IsActive ? "Activated" : "Deactivated";
        await _audit.WriteAsync(action, "Company", company.CompanyId.ToString(),
            $"{action} company '{company.CompanyName}'.");

        return Ok(new
        {
            message = $"Company '{company.CompanyName}' has been {action.ToLowerInvariant()}.",
            isActive = company.IsActive
        });
    }
}