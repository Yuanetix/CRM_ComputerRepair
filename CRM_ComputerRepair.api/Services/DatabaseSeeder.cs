using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM_ComputerRepair.api.Services;

/// <summary>
/// Idempotent startup seeder. Applies migrations to the master and tenant
/// databases, creates demo roles/users, the demo company + tenant database row,
/// loyalty programs (with full eligibility/reward configuration), a customer
/// base of ~80 actors, 200+ repair requests and 200+ payments (the "transactions")
/// spread across the last 12 months, plus interactions, follow-ups, status history
/// and loyalty membership — enough real history for the BI dashboard, retention
/// engine basis text and loyalty recommendations to be meaningful.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task ResetAndSeedAllAsync(IServiceProvider sp, ILogger logger)
    {
        var master = sp.GetRequiredService<MasterCrmDbContext>();
        var factory = sp.GetRequiredService<ITenantDbContextFactory>();
        var users = sp.GetRequiredService<UserManager<User>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();

        logger.LogInformation("Applying master database migrations...");
        await master.Database.MigrateAsync();

        // 1. Reset identity: exactly ONE super admin account, plus the 3 tenant company accounts
        await ResetAndSeedIdentityAsync(users, roles, logger);

        // 2. Reset master CRM companies & subscriptions
        await ResetAndSeedCompaniesAsync(master, logger);

        // 3. Loyalty programs
        var seededPrograms = await SeedLoyaltyProgramsAsync(master);

        // 4. Reset & seed all 3 tenant databases
        var tenantDatabases = await master.CompanyDatabases
            .Where(d => d.IsActive)
            .ToListAsync();

        foreach (var tdb in tenantDatabases)
        {
            logger.LogInformation("Wiping and reseeding tenant database for Company {CompanyId} ({DatabaseName})...", tdb.CompanyId, tdb.DatabaseName);
            await using var tenant = await factory.CreateAsync(tdb.CompanyId);
            await tenant.Database.MigrateAsync();

            // Clean slate wipe
            tenant.FollowUps.RemoveRange(tenant.FollowUps);
            tenant.CustomerInteractions.RemoveRange(tenant.CustomerInteractions);
            tenant.RepairParts.RemoveRange(tenant.RepairParts);
            tenant.Payments.RemoveRange(tenant.Payments);
            tenant.RepairStatusHistories.RemoveRange(tenant.RepairStatusHistories);
            tenant.RepairRequests.RemoveRange(tenant.RepairRequests);
            tenant.Parts.RemoveRange(tenant.Parts);
            tenant.Suppliers.RemoveRange(tenant.Suppliers);
            tenant.Devices.RemoveRange(tenant.Devices);
            tenant.RetentionEmailLogs.RemoveRange(tenant.RetentionEmailLogs);
            tenant.RetentionRequests.RemoveRange(tenant.RetentionRequests);
            tenant.RetentionSettings.RemoveRange(tenant.RetentionSettings);
            tenant.RetentionEmailTemplates.RemoveRange(tenant.RetentionEmailTemplates);
            tenant.Customers.RemoveRange(tenant.Customers);
            tenant.SyncQueue.RemoveRange(tenant.SyncQueue);
            await tenant.SaveChangesAsync();

            // Seed fresh realistic data (over 1,600 realistic records)
            await SeedTenantAsync(tenant, master, seededPrograms, tdb.CompanyId);
            await SeedRetentionDefaultsAsync(tenant);
        }

        // 5. Loyalty memberships
        try
        {
            await SeedMissingLoyaltyAccountsAsync(master, factory, seededPrograms, tenantDatabases, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed seeding loyalty memberships into master database");
        }

        logger.LogInformation("All system databases have been reset and seeded with valid realistic data.");
    }

    public static async Task SeedAsync(IServiceProvider sp, ILogger<Program> logger)
    {
        var master = sp.GetRequiredService<MasterCrmDbContext>();

        // ── Migrations (both databases) ──
        logger.LogInformation("Applying master database migrations...");
        await master.Database.MigrateAsync();

        // ── Identity roles + system users ──
        var users = sp.GetRequiredService<UserManager<User>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedIdentityAsync(users, roles);

        // ── 3 Companies + tenant database rows ──
        await SeedCompanyAsync(master);

        // ── Loyalty programs, terms, subscription ──
        var seededPrograms = await SeedLoyaltyProgramsAsync(master);

        // ── Tenant databases (all active companies) ──
        var factory = sp.GetRequiredService<ITenantDbContextFactory>();
        var tenantDatabases = await master.CompanyDatabases
            .Where(d => d.IsActive)
            .ToListAsync();

        foreach (var tdb in tenantDatabases)
        {
            try
            {
                logger.LogInformation("Applying tenant database migrations for Company {CompanyId} ({DatabaseName})...", tdb.CompanyId, tdb.DatabaseName);
                await using var tenant = await factory.CreateAsync(tdb.CompanyId);
                await tenant.Database.MigrateAsync();

                int existingCustCount = await tenant.Customers.CountAsync();
                if (existingCustCount < 200)
                {
                    logger.LogInformation("Tenant database for Company {CompanyId} has only {Count} customers (needs >= 200). Reseeding with full 200+ dataset...", tdb.CompanyId, existingCustCount);
                    tenant.FollowUps.RemoveRange(tenant.FollowUps);
                    tenant.CustomerInteractions.RemoveRange(tenant.CustomerInteractions);
                    tenant.RepairParts.RemoveRange(tenant.RepairParts);
                    tenant.Payments.RemoveRange(tenant.Payments);
                    tenant.RepairStatusHistories.RemoveRange(tenant.RepairStatusHistories);
                    tenant.RepairRequests.RemoveRange(tenant.RepairRequests);
                    tenant.Parts.RemoveRange(tenant.Parts);
                    tenant.Suppliers.RemoveRange(tenant.Suppliers);
                    tenant.Devices.RemoveRange(tenant.Devices);
                    tenant.RetentionEmailLogs.RemoveRange(tenant.RetentionEmailLogs);
                    tenant.RetentionRequests.RemoveRange(tenant.RetentionRequests);
                    tenant.RetentionSettings.RemoveRange(tenant.RetentionSettings);
                    tenant.RetentionEmailTemplates.RemoveRange(tenant.RetentionEmailTemplates);
                    tenant.Customers.RemoveRange(tenant.Customers);
                    tenant.SyncQueue.RemoveRange(tenant.SyncQueue);
                    await tenant.SaveChangesAsync();

                    await SeedTenantAsync(tenant, master, seededPrograms, tdb.CompanyId);
                }

                await SeedRetentionDefaultsAsync(tenant);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed migrating or seeding tenant database for Company {CompanyId} ({DatabaseName})", tdb.CompanyId, tdb.DatabaseName);
            }
        }

        // ── Ensure loyalty memberships exist across tenant customers ──
        try
        {
            await SeedMissingLoyaltyAccountsAsync(master, factory, seededPrograms, tenantDatabases, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed seeding loyalty memberships into master database");
        }

        logger.LogInformation("Database seeding completed successfully.");
    }

    // ═══════════════════════ Identity ═══════════════════════

    // ═══════════════════════ Identity ═══════════════════════

    private static async Task ResetAndSeedIdentityAsync(
        UserManager<User> users, RoleManager<IdentityRole> roles, ILogger logger)
    {
        var roleNames = new[] { "Super Admin", "Admin", "Manager", "Staff" };
        foreach (var r in roleNames)
        {
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));
        }

        var allowedUsers = new[]
        {
            // Exactly ONE super admin account -> DB_MasterCRM
            new { UserName = "superadmin", Email = "admin@fixorycrm.com", Password = "SuperAdmin@123", First = "Super", Last = "Admin", Role = "Super Admin", CompanyId = (int?)1 },

            // Fixtech accounts (Company 1) -> DB_Fixtech
            new { UserName = "fixtech", Email = "admin@fixtech.ph", Password = "Fixtech@123", First = "Fixtech", Last = "Administrator", Role = "Admin", CompanyId = (int?)1 },
            new { UserName = "fixtechManager", Email = "manager@fixtech.ph", Password = "Fixtech@123", First = "Fixtech", Last = "Manager", Role = "Manager", CompanyId = (int?)1 },
            new { UserName = "fixtechStaff", Email = "staff@fixtech.ph", Password = "Fixtech@123", First = "Fixtech", Last = "Staff", Role = "Staff", CompanyId = (int?)1 },

            // ByteCare accounts (Company 2) -> DB_Bytecare
            new { UserName = "bytecare", Email = "admin@bytecare.ph", Password = "Bytecare@123", First = "ByteCare", Last = "Administrator", Role = "Admin", CompanyId = (int?)2 },
            new { UserName = "bytecareManager", Email = "manager@bytecare.ph", Password = "Bytecare@123", First = "ByteCare", Last = "Manager", Role = "Manager", CompanyId = (int?)2 },
            new { UserName = "bytecareStaff", Email = "staff@bytecare.ph", Password = "Bytecare@123", First = "ByteCare", Last = "Staff", Role = "Staff", CompanyId = (int?)2 },

            // TechRevive accounts (Company 3) -> DB_Techrevive
            new { UserName = "techrevive", Email = "admin@techrevive.ph", Password = "Techrevive@123", First = "TechRevive", Last = "Administrator", Role = "Admin", CompanyId = (int?)3 },
            new { UserName = "techreviveManager", Email = "manager@techrevive.ph", Password = "Techrevive@123", First = "TechRevive", Last = "Manager", Role = "Manager", CompanyId = (int?)3 },
            new { UserName = "techreviveStaff", Email = "staff@techrevive.ph", Password = "Techrevive@123", First = "TechRevive", Last = "Staff", Role = "Staff", CompanyId = (int?)3 },
        };

        var allowedUsernames = allowedUsers.Select(x => x.UserName.ToLowerInvariant()).ToHashSet();

        // Remove any legacy users not in allowed list
        var existing = await users.Users.ToListAsync();
        foreach (var u in existing)
        {
            if (!allowedUsernames.Contains((u.UserName ?? "").ToLowerInvariant()))
            {
                logger.LogInformation("Removing legacy user: {Username}", u.UserName);
                await users.DeleteAsync(u);
            }
        }

        // Create or update allowed users
        foreach (var d in allowedUsers)
        {
            var u = await users.FindByNameAsync(d.UserName);
            if (u == null)
            {
                u = new User
                {
                    UserName = d.UserName,
                    Email = d.Email,
                    EmailConfirmed = true,
                    FirstName = d.First,
                    LastName = d.Last,
                    CompanyId = d.CompanyId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var createRes = await users.CreateAsync(u, d.Password);
                if (createRes.Succeeded)
                {
                    await users.AddToRoleAsync(u, d.Role);
                }
            }
            else
            {
                u.CompanyId = d.CompanyId;
                u.Email = d.Email;
                u.FirstName = d.First;
                u.LastName = d.Last;
                u.IsActive = true;
                await users.UpdateAsync(u);

                var curRoles = await users.GetRolesAsync(u);
                if (!curRoles.Contains(d.Role))
                {
                    await users.RemoveFromRolesAsync(u, curRoles);
                    await users.AddToRoleAsync(u, d.Role);
                }

                var token = await users.GeneratePasswordResetTokenAsync(u);
                await users.ResetPasswordAsync(u, token, d.Password);
            }
        }
    }

    private static async Task SeedIdentityAsync(
        UserManager<User> users, RoleManager<IdentityRole> roles)
    {
        var roleNames = new[] { "Super Admin", "Admin", "Manager", "Staff" };
        foreach (var r in roleNames)
        {
            if (!await roles.RoleExistsAsync(r))
                await roles.CreateAsync(new IdentityRole(r));
        }

        var demoUsers = new[]
        {
            new { UserName = "superadmin", Email = "admin@fixorycrm.com", Password = "SuperAdmin@123", First = "Super", Last = "Admin", Role = "Super Admin", CompanyId = (int?)1 },
            new { UserName = "fixtech", Email = "admin@fixtech.ph", Password = "Fixtech@123", First = "Fixtech", Last = "Administrator", Role = "Admin", CompanyId = (int?)1 },
            new { UserName = "fixtechManager", Email = "manager@fixtech.ph", Password = "Fixtech@123", First = "Fixtech", Last = "Manager", Role = "Manager", CompanyId = (int?)1 },
            new { UserName = "fixtechStaff", Email = "staff@fixtech.ph", Password = "Fixtech@123", First = "Fixtech", Last = "Staff", Role = "Staff", CompanyId = (int?)1 },
            new { UserName = "bytecare", Email = "admin@bytecare.ph", Password = "Bytecare@123", First = "ByteCare", Last = "Administrator", Role = "Admin", CompanyId = (int?)2 },
            new { UserName = "bytecareManager", Email = "manager@bytecare.ph", Password = "Bytecare@123", First = "ByteCare", Last = "Manager", Role = "Manager", CompanyId = (int?)2 },
            new { UserName = "bytecareStaff", Email = "staff@bytecare.ph", Password = "Bytecare@123", First = "ByteCare", Last = "Staff", Role = "Staff", CompanyId = (int?)2 },
            new { UserName = "techrevive", Email = "admin@techrevive.ph", Password = "Techrevive@123", First = "TechRevive", Last = "Administrator", Role = "Admin", CompanyId = (int?)3 },
            new { UserName = "techreviveManager", Email = "manager@techrevive.ph", Password = "Techrevive@123", First = "TechRevive", Last = "Manager", Role = "Manager", CompanyId = (int?)3 },
            new { UserName = "techreviveStaff", Email = "staff@techrevive.ph", Password = "Techrevive@123", First = "TechRevive", Last = "Staff", Role = "Staff", CompanyId = (int?)3 },
        };

        var allowedUsernames = demoUsers.Select(x => x.UserName.ToLowerInvariant()).ToHashSet();

        // Prune any legacy or unapproved accounts
        var existing = await users.Users.ToListAsync();
        foreach (var u in existing)
        {
            if (!allowedUsernames.Contains((u.UserName ?? "").ToLowerInvariant()))
            {
                await users.DeleteAsync(u);
            }
        }

        foreach (var d in demoUsers)
        {
            var user = await users.FindByNameAsync(d.UserName);
            if (user == null)
            {
                user = new User
                {
                    UserName = d.UserName,
                    Email = d.Email,
                    EmailConfirmed = true,
                    FirstName = d.First,
                    LastName = d.Last,
                    CompanyId = d.CompanyId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await users.CreateAsync(user, d.Password);
                if (result.Succeeded)
                    await users.AddToRoleAsync(user, d.Role);
            }
            else
            {
                user.CompanyId = d.CompanyId;
                user.Email = d.Email;
                user.FirstName = d.First;
                user.LastName = d.Last;
                user.IsActive = true;
                await users.UpdateAsync(user);

                var curRoles = await users.GetRolesAsync(user);
                if (!curRoles.Contains(d.Role))
                {
                    await users.RemoveFromRolesAsync(user, curRoles);
                    await users.AddToRoleAsync(user, d.Role);
                }

                var token = await users.GeneratePasswordResetTokenAsync(user);
                await users.ResetPasswordAsync(user, token, d.Password);
            }
        }
    }

    // ═══════════════════════ Master ═══════════════════════

    private static async Task ResetAndSeedCompaniesAsync(MasterCrmDbContext master, ILogger logger)
    {
        // 1. Subscription Plans
        var starter = await master.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionName == "Starter Plan");
        if (starter == null)
        {
            starter = new Subscription
            {
                SubscriptionName = "Starter Plan",
                PricePerMonth = 999.00m,
                DurationMonths = 1,
                Duration = "1 Month",
                MaxUsers = 5,
                MaxDevices = 100,
                EnableMultiBranching = false,
                Description = "Essential repair intake, customer tracking, and diagnostic ticketing for small repair shops.",
                BillingCycle = "Monthly",
                IsActive = true,
                IsArchived = false,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow.AddDays(365),
                CreatedAt = DateTime.UtcNow
            };
            master.Subscriptions.Add(starter);
        }
        else
        {
            starter.EnableMultiBranching = false;
        }

        var pro = await master.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionName == "Professional Plan");
        if (pro == null)
        {
            pro = new Subscription
            {
                SubscriptionName = "Professional Plan",
                PricePerMonth = 2499.00m,
                DurationMonths = 1,
                Duration = "1 Month",
                MaxUsers = 15,
                MaxDevices = 500,
                EnableMultiBranching = false,
                Description = "Full CRM power including automated retention email campaigns, customer loyalty rewards, and PDF exports.",
                BillingCycle = "Monthly",
                IsActive = true,
                IsArchived = false,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow.AddDays(365),
                CreatedAt = DateTime.UtcNow
            };
            master.Subscriptions.Add(pro);
        }
        else
        {
            pro.EnableMultiBranching = false;
        }

        var ent = await master.Subscriptions.FirstOrDefaultAsync(s => s.SubscriptionName == "Enterprise Multi-Branch");
        if (ent == null)
        {
            ent = new Subscription
            {
                SubscriptionName = "Enterprise Multi-Branch",
                PricePerMonth = 4999.00m,
                DurationMonths = 12,
                Duration = "12 Months (Annual)",
                MaxUsers = 50,
                MaxDevices = 2500,
                EnableMultiBranching = true,
                Description = "Multi-branch store synchronization, unlimited repair technicians, priority database cluster, and custom retention templates.",
                BillingCycle = "Yearly",
                IsActive = true,
                IsArchived = false,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow.AddDays(365),
                CreatedAt = DateTime.UtcNow
            };
            master.Subscriptions.Add(ent);
        }
        else
        {
            ent.EnableMultiBranching = true;
        }

        await master.SaveChangesAsync();

        // 2. Remove legacy non-conforming companies
        var legacyCompanies = await master.Companies
            .Where(c => c.CompanyId > 3 || (c.CompanyCode != "FIXTECH" && c.CompanyCode != "BYTECARE" && c.CompanyCode != "TECHREVIVE" && c.CompanyId != 1 && c.CompanyId != 2 && c.CompanyId != 3))
            .ToListAsync();

        foreach (var extra in legacyCompanies)
        {
            var extraLpIds = await master.LoyaltyPrograms.Where(lp => lp.CompanyId == extra.CompanyId).Select(lp => lp.LoyaltyProgramId).ToListAsync();
            var extraLoyaltyAccs = await master.CustomerLoyaltyAccounts.Where(a => extraLpIds.Contains(a.LoyaltyProgramId)).ToListAsync();
            master.CustomerLoyaltyAccounts.RemoveRange(extraLoyaltyAccs);

            var extraLoyalty = await master.LoyaltyPrograms.Where(lp => lp.CompanyId == extra.CompanyId).ToListAsync();
            master.LoyaltyPrograms.RemoveRange(extraLoyalty);

            var extraDevices = await master.Devices.Where(d => d.CompanyId == extra.CompanyId).ToListAsync();
            master.Devices.RemoveRange(extraDevices);

            var extraSubs = await master.Subscriptions.Where(s => s.CompanyId == extra.CompanyId).ToListAsync();
            foreach (var sub in extraSubs)
            {
                sub.CompanyId = null;
            }

            var extraDbs = await master.CompanyDatabases.Where(d => d.CompanyId == extra.CompanyId).ToListAsync();
            master.CompanyDatabases.RemoveRange(extraDbs);
            master.Companies.Remove(extra);
        }
        await master.SaveChangesAsync();

        // 3. Ensure Company 1: Fixtech
        var c1 = await master.Companies.FirstOrDefaultAsync(c => c.CompanyId == 1);
        if (c1 == null)
        {
            c1 = new Company { CompanyId = 1 };
            master.Companies.Add(c1);
        }
        c1.CompanyCode = "FIXTECH";
        c1.CompanyName = "Fixtech Computer Solutions";
        c1.ContactFirstName = "Eduardo";
        c1.ContactLastName = "Santos";
        c1.ContactPhone = "+63 917 555 1010";
        c1.ContactEmail = "contact@fixtech.ph";
        c1.Address = "Unit 204 Cyberzone Gilmore, Aurora Blvd";
        c1.City = "Quezon City";
        c1.StateOrProvince = "Metro Manila";
        c1.PostalCode = "1112";
        c1.Country = "Philippines";
        c1.SubscriptionId = starter.SubscriptionId;
        c1.IsActive = true;
        c1.HasAcceptedTerms = true;
        c1.TermsAcceptedAt = DateTime.UtcNow;

        // Ensure Company 2: bytecare
        var c2 = await master.Companies.FirstOrDefaultAsync(c => c.CompanyId == 2);
        if (c2 == null)
        {
            c2 = new Company { CompanyId = 2 };
            master.Companies.Add(c2);
        }
        c2.CompanyCode = "BYTECARE";
        c2.CompanyName = "ByteCare Repairs & IT Services";
        c2.ContactFirstName = "Carmela";
        c2.ContactLastName = "Reyes";
        c2.ContactPhone = "+63 918 555 2020";
        c2.ContactEmail = "support@bytecare.ph";
        c2.Address = "Level 3 Ayala Malls Circuit, Theater Drive";
        c2.City = "Makati";
        c2.StateOrProvince = "Metro Manila";
        c2.PostalCode = "1207";
        c2.Country = "Philippines";
        c2.SubscriptionId = pro.SubscriptionId;
        c2.IsActive = true;
        c2.HasAcceptedTerms = true;
        c2.TermsAcceptedAt = DateTime.UtcNow;

        // Ensure Company 3: techrevive
        var c3 = await master.Companies.FirstOrDefaultAsync(c => c.CompanyId == 3);
        if (c3 == null)
        {
            c3 = new Company { CompanyId = 3 };
            master.Companies.Add(c3);
        }
        c3.CompanyCode = "TECHREVIVE";
        c3.CompanyName = "TechRevive Systems & Electronics";
        c3.ContactFirstName = "Rodrigo";
        c3.ContactLastName = "Navarro";
        c3.ContactPhone = "+63 920 555 3030";
        c3.ContactEmail = "info@techrevive.ph";
        c3.Address = "G/F High Street South Corporate Plaza, 26th St";
        c3.City = "Taguig";
        c3.StateOrProvince = "Metro Manila";
        c3.PostalCode = "1634";
        c3.Country = "Philippines";
        c3.SubscriptionId = ent.SubscriptionId;
        c3.IsActive = true;
        c3.HasAcceptedTerms = true;
        c3.TermsAcceptedAt = DateTime.UtcNow;

        await master.SaveChangesAsync();

        // 4. Map CompanyDatabases
        var dbMap = new[]
        {
            new { CompId = 1, DbName = "DB_TenantRepairs_Company1" },
            new { CompId = 2, DbName = "DB_TenantRepairs_Company2" },
            new { CompId = 3, DbName = "DB_TenantRepairs_Company3" },
        };

        foreach (var dm in dbMap)
        {
            var cdb = await master.CompanyDatabases.FirstOrDefaultAsync(d => d.CompanyId == dm.CompId);
            if (cdb == null)
            {
                master.CompanyDatabases.Add(new CompanyDatabase
                {
                    CompanyId = dm.CompId,
                    ServerName = @"(localdb)\MSSQLLocalDB",
                    DatabaseName = dm.DbName,
                    CredentialKey = "",
                    IsActive = true
                });
            }
            else
            {
                cdb.ServerName = @"(localdb)\MSSQLLocalDB";
                cdb.DatabaseName = dm.DbName;
                cdb.IsActive = true;
            }
        }

        if (!await master.TermsAndConditionsSet.AnyAsync(t => t.Title == "Standard Service Agreement"))
        {
            master.TermsAndConditionsSet.Add(new TermsAndConditions
            {
                Title = "Standard Service Agreement",
                Content =
                    "1. A service fee applies to all repairs and is quoted only after diagnosis.\r\n" +
                    "2. Parts replaced during repair are covered by manufacturer warranty.\r\n" +
                    "3. Labor warranty of 30 days applies on completed repairs.\r\n" +
                    "4. Data recovery is attempted on a best-effort basis.\r\n" +
                    "5. Unclaimed units will be considered abandoned after 60 days.",
                Version = DateTime.UtcNow,
                IsActive = true,
                CreatedByUserId = "system",
                CreatedAt = DateTime.UtcNow
            });
        }

        await master.SaveChangesAsync();
    }

    private static async Task SeedCompanyAsync(MasterCrmDbContext master)
    {
        await ResetAndSeedCompaniesAsync(master, null!);
    }

    private static async Task<List<LoyaltyProgram>> SeedLoyaltyProgramsAsync(MasterCrmDbContext master)
    {
        // 1. Ensure any legacy programs are assigned to Company 1
        var legacy = await master.LoyaltyPrograms.Where(p => p.CompanyId == null).ToListAsync();
        foreach (var leg in legacy)
            leg.CompanyId = 1;
        if (legacy.Count > 0)
            await master.SaveChangesAsync();

        var programs = new List<LoyaltyProgram>();

        // 2. Company 1 (Fixory) programs
        if (!await master.LoyaltyPrograms.AnyAsync(p => p.CompanyId == 1 && p.ProgramName == "Fixory Rewards Club"))
        {
            var p = new LoyaltyProgram
            {
                CompanyId = 1,
                ProgramName = "Fixory Rewards Club",
                Description =
                    "Customers with at least 3 completed repairs and \u20b11,500 in total spending " +
                    "receive a 10% discount on their next service.",
                PointsPerPeso = 1,
                DiscountPercentage = 10,
                MinimumSpend = 500,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                EndDate = DateTime.UtcNow.AddMonths(6),
                PointsValidityDays = 365,
                RedeemPointsRequired = 500,
                MinTransactions = 3,
                MinTotalSpent = 1500,
                MaxInactiveDays = 120,
                MinVisitsPerPeriod = null,
                VisitPeriodDays = null,
                RewardType = LoyaltyRewardType.DiscountPercent,
                RewardValue = 10,
                MaxRedemptionsPerCustomer = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            master.LoyaltyPrograms.Add(p);
            programs.Add(p);
        }

        if (!await master.LoyaltyPrograms.AnyAsync(p => p.CompanyId == 1 && p.ProgramName == "VIP Service Club"))
        {
            var p = new LoyaltyProgram
            {
                CompanyId = 1,
                ProgramName = "VIP Service Club",
                Description =
                    "High-value customers (\u20b110,000+ lifetime spend) get a FREE service " +
                    "worth up to \u20b11,500 once a year.",
                PointsPerPeso = 2,
                DiscountPercentage = 0,
                MinimumSpend = 10000,
                StartDate = DateTime.UtcNow.AddMonths(-12),
                EndDate = DateTime.UtcNow.AddMonths(6),
                PointsValidityDays = 365,
                RedeemPointsRequired = 2000,
                MinTransactions = 6,
                MinTotalSpent = 10000,
                MaxInactiveDays = 365,
                MinVisitsPerPeriod = null,
                VisitPeriodDays = null,
                RewardType = LoyaltyRewardType.FreeService,
                RewardValue = 1500,
                MaxRedemptionsPerCustomer = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            master.LoyaltyPrograms.Add(p);
            programs.Add(p);
        }

        if (!await master.LoyaltyPrograms.AnyAsync(p => p.CompanyId == 1 && p.ProgramName == "Monthly Visitor Boost"))
        {
            var p = new LoyaltyProgram
            {
                CompanyId = 1,
                ProgramName = "Monthly Visitor Boost",
                Description =
                    "Customers visiting 2+ times within 60 days earn a 1.5\u00d7 points multiplier " +
                    "on their next transaction.",
                PointsPerPeso = 1,
                DiscountPercentage = 0,
                MinimumSpend = 300,
                StartDate = DateTime.UtcNow.AddMonths(-3),
                EndDate = DateTime.UtcNow.AddMonths(6),
                PointsValidityDays = 90,
                RedeemPointsRequired = 300,
                MinTransactions = null,
                MinTotalSpent = 500,
                MaxInactiveDays = 90,
                MinVisitsPerPeriod = 2,
                VisitPeriodDays = 60,
                RewardType = LoyaltyRewardType.PointsMultiplier,
                RewardValue = 1.5m,
                MaxRedemptionsPerCustomer = 6,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            master.LoyaltyPrograms.Add(p);
            programs.Add(p);
        }

        // 3. For every other company in Master DB, ensure it has its OWN distinct loyalty programs!
        var otherCompanies = await master.Companies.AsNoTracking().Where(c => c.CompanyId != 1).ToListAsync();
        foreach (var comp in otherCompanies)
        {
            bool hasPrograms = await master.LoyaltyPrograms.AnyAsync(p => p.CompanyId == comp.CompanyId);
            if (!hasPrograms)
            {
                var p1 = new LoyaltyProgram
                {
                    CompanyId = comp.CompanyId,
                    ProgramName = $"{comp.CompanyName} Rewards Club",
                    Description = $"Earn 1 loyalty point per \u20b11 spent. Unlock 10% off your next repair after 3 completed visits and \u20b11,500 total spending.",
                    PointsPerPeso = 1,
                    DiscountPercentage = 10,
                    MinimumSpend = 500,
                    StartDate = DateTime.UtcNow.AddMonths(-3),
                    EndDate = DateTime.UtcNow.AddMonths(9),
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
                };
                master.LoyaltyPrograms.Add(p1);
                programs.Add(p1);

                var p2 = new LoyaltyProgram
                {
                    CompanyId = comp.CompanyId,
                    ProgramName = $"{comp.CompanyName} VIP Care Tier",
                    Description = $"Exclusive tier for frequent clients (\u20b18,000+ lifetime spend). Entitled to a free annual maintenance diagnostic.",
                    PointsPerPeso = 2,
                    DiscountPercentage = 0,
                    MinimumSpend = 8000,
                    StartDate = DateTime.UtcNow.AddMonths(-6),
                    EndDate = DateTime.UtcNow.AddMonths(6),
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
                };
                master.LoyaltyPrograms.Add(p2);
                programs.Add(p2);
            }
        }

        await master.SaveChangesAsync();

        return await master.LoyaltyPrograms.AsNoTracking().ToListAsync();
    }

    // ═══════════════════════ Tenant ═══════════════════════

    private static async Task SeedTenantAsync(
        TenantCrmDbContext tenant,
        MasterCrmDbContext master,
        List<LoyaltyProgram> programs,
        int companyId = 1)
    {
        var rng = new Random(2026 + companyId);
        var now = DateTime.UtcNow;

        // ── Devices ──
        var devices = new List<Device>();
        var deviceDefs = new (string Code, string Name, string Type, string Brand, string Model)[]
        {
            ("DEV-LT-01", "Dell Latitude 5410 Laptop",  "Laptop",  "Dell", "Latitude 5410"),
            ("DEV-LT-02", "HP ProBook 450 G8 Laptop",    "Laptop",  "HP",    "ProBook 450 G8"),
            ("DEV-LT-03", "Acer Aspire 5 Laptop",        "Laptop",  "Acer",  "Aspire 5 A515"),
            ("DEV-DT-01", "ASUS Prime Desktop",          "Desktop", "ASUS",  "Prime B450"),
            ("DEV-MB-01", "MacBook Air M1",              "Laptop",  "Apple", "MacBook Air A2337"),
            ("DEV-PC-01", "Custom Gaming PC",            "Desktop", "Custom","RTX 3060 Build"),
            ("DEV-PN-01", "Lenovo ThinkPad T14",         "Laptop",  "Lenovo","ThinkPad T14"),
            ("DEV-IP-01", "iPhone 13 Pro",               "Phone",   "Apple", "iPhone 13 Pro"),
            ("DEV-SA-01", "Samsung S22 Ultra",           "Phone",   "Samsung","Galaxy S22 Ultra"),
            ("DEV-AP-01", "iMac 24\" M1",                "Desktop", "Apple", "iMac 24 A2438"),
            ("DEV-LA-01", "Sony VAIO",                   "Laptop",  "Sony",  "VAIO SVF152"),
            ("DEV-CB-01", "Razer Blade 15",              "Laptop",  "Razer", "Blade 15 Advanced"),
            ("DEV-LT-04", "Lenovo Legion 5 Gaming",      "Laptop",  "Lenovo","Legion 5 15ACH6"),
            ("DEV-LT-05", "ASUS ZenBook 14",             "Laptop",  "ASUS",  "UX425EA"),
            ("DEV-LT-06", "MSI Modern 14",               "Laptop",  "MSI",   "B11M"),
            ("DEV-DT-02", "Dell OptiPlex 7080 Micro",    "Desktop", "Dell",  "OptiPlex 7080"),
            ("DEV-MB-02", "MacBook Pro 16\" M2 Max",     "Laptop",  "Apple", "MacBook Pro A2780"),
            ("DEV-IP-02", "iPad Pro 11\" M2",            "Tablet",  "Apple", "iPad Pro A2759"),
            ("DEV-SA-02", "Samsung Galaxy Tab S8",       "Tablet",  "Samsung","Galaxy Tab S8"),
            ("DEV-PC-02", "Office Workstation Core i7",   "Desktop", "Dell",  "Precision 3650")
        };

        foreach (var d in deviceDefs)
        {
            devices.Add(new Device
            {
                CompanyId = companyId,
                DeviceCode = d.Code,
                DeviceName = d.Name,
                DeviceType = d.Type,
                Brand = d.Brand,
                Model = d.Model,
                SerialNumber = $"SN-{d.Code}-{rng.Next(100000, 999999)}",
                PurchasePrice = rng.Next(30000, 120000),
                PurchaseDate = now.AddDays(-rng.Next(30, 400)),
                WarrantyStatus = rng.Next(0, 3) == 0 ? "Out of Warranty" : "Under Warranty",
                WarrantyExpiry = now.AddDays(rng.Next(60, 500)),
                Status = "Operational",
                IsActive = true,
                CreatedAt = now
            });
        }
        tenant.Devices.AddRange(devices);

        // ── Customers (220) ──
        var firstNames = new[]
        {
            "Maria", "Jose", "Juan", "Ana", "Pedro", "Liza", "Carlo", "Rosa", "Miguel", "Elena",
            "Marco", "Sofia", "Danny", "Grace", "Ian", "Joyce", "Kevin", "Lorna", "Manny", "Nina",
            "Oscar", "Paula", "Ramon", "Sandra", "Tony", "Ursula", "Victor", "Wendy", "Xander", "Yolly",
            "Zandro", "Alma", "Berto", "Christine", "Dindo", "Erica", "Ferdie", "Gina", "Hector", "Isabel",
            "Jasper", "Kristine", "Leo", "Mila", "Nonoy", "Olive", "Perry", "Queen", "Rico", "Sheryl",
            "Tim", "Vina", "Willie", "Yanie", "Zed", "Aileen", "Brando", "Connie", "Dante", "Fely",
            "Gilbert", "Harold", "Imelda", "Jun", "Kayla", "Louie", "May", "Nestor", "Odessa", "Paolo",
            "Rafael", "Sonia", "Teresa", "Vince", "Winston", "Yumi", "Amon", "Bella", "Cesar", "Divina",
            "Eduardo", "Francis", "Gloria", "Henry", "Irene", "Joel", "Karen", "Lorenzo", "Mercedes", "Noel",
            "Orlando", "Patricia", "Quirino", "Rowena", "Salvador", "Thelma", "Ulysses", "Valerie", "Wilfredo", "Ximena",
            "Yolanda", "Zachary", "Abigail", "Benjie", "Clarissa", "Danilo", "Esther", "Froilan", "Gemma", "Hermie"
        };

        var lastNames = new[]
        {
            "Santos", "Reyes", "Cruz", "Bautista", "Ocampo", "Dela Cruz", "Garcia", "Mendoza",
            "Torres", "Flores", "Ramos", "Aquino", "Domingo", "Rosario", "Villanueva", "Navarro",
            "Salazar", "Perez", "Castillo", "Lopez", "Fernandez", "Gonzales", "Rivera", "Castro",
            "Valdez", "Aguilar", "Morales", "Alvarez", "Rojas", "Del Rosario", "Sison", "Tan",
            "Lim", "Chua", "Co", "Uy", "Sy", "Dizon", "Espinoza", "Marquez", "Manalo", "Mercado",
            "Soriano", "Pascual", "David", "Macaraeg", "Pineda", "Cabrera", "Tolentino", "Padilla", "Bernardo"
        };

        var customers = new List<Customer>();
        for (int i = 0; i < 220; i++)
        {
            var joined = now;
            var roll = i % 100;
            if (roll < 8) joined = now.AddDays(-rng.Next(2, 28));                    // 8% new this month
            else if (roll < 40) joined = now.AddDays(-rng.Next(30, 180));
            else if (roll < 70) joined = now.AddDays(-rng.Next(180, 365));
            else joined = now.AddDays(-rng.Next(365, 700));

            var fn = firstNames[i % firstNames.Length];
            var ln = lastNames[(i * 3 + rng.Next(lastNames.Length)) % lastNames.Length];
            var cleanLn = ln.ToLowerInvariant().Replace(" ", "").Replace(".", "");
            var cleanFn = fn.ToLowerInvariant();

            var emailDomains = new[] { "gmail.com", "yahoo.com", "outlook.com", "icloud.com" };
            var selectedDomain = emailDomains[rng.Next(emailDomains.Length)];

            var cities = new[] { "Makati", "Manila", "Pasig", "Quezon City", "Taguig", "Mandaluyong", "San Juan", "Parañaque" };
            var streets = new[] { "Pearl", "Rizal", "Bonifacio", "Quezon", "Luna", "Mahogany", "Shaw Blvd", "Gilmore", "Aurora Blvd", "Ayala Ave", "Kalayaan", "Taft Ave" };
            var selectedCity = cities[rng.Next(cities.Length)];
            customers.Add(new Customer
            {
                FirstName = fn,
                LastName = ln,
                Email = $"{cleanFn}.{cleanLn}{i + 1}@{selectedDomain}",
                Phone = $"09{rng.Next(11, 20)}{rng.Next(1000000, 9999999)}",
                Address = $"{rng.Next(12, 599)} {streets[rng.Next(streets.Length)]} St.",
                City = selectedCity,
                StateOrProvince = "Metro Manila",
                PostalCode = $"{rng.Next(1000, 1800)}",
                Country = "Philippines",
                LoyaltyPoints = 0,
                IsActive = true,
                CreatedAt = joined
            });
        }

        // Selected customers intentionally have zero repairs — for the "never purchased" segment.
        var neverPurchased = new[] { 3, 21, 44, 67, 102, 145, 189 };
        var inactiveIndexes = Enumerable.Range(0, 220).Where(i => !neverPurchased.Contains(i) && i % 17 == 0).ToList();

        tenant.Customers.AddRange(customers);
        await tenant.SaveChangesAsync();

        // ── Suppliers + parts ──
        var suppliers = new List<Supplier>();
        var supplierDefs = new (string Code, string Name, string First, string Last, string Num, string Mail, string Street, string City, string Postal)[]
        {
            ("SUP-01", "LaptopParts PH",   "Andres", "Lim",    "09171234567", "sales@laptopparts.ph", "Unit 401 Techno Plaza", "Quezon City", "1100"),
            ("SUP-02", "BatteryPro Supply", "Marlon", "Uy",     "09182223344", "orders@batterypro.ph", "Bldg 2 South Superhighway", "Makati", "1233"),
            ("SUP-03", "ScreenFix Distributor", "Grace", "Co",  "09175556677", "gc@screenfix.ph", "88 Aurora Blvd", "Quezon City", "1102"),
            ("SUP-04", "PCHub Retail",      "Ben", "Torres",   "09178889900", "ben@pchub.ph", "Gilmore IT Center", "Quezon City", "1112"),
            ("SUP-05", "SSD Mart",          "Cathy", "Tan",    "09173334455", "sales@ssdmart.ph", "15 Shaw Blvd", "Mandaluyong", "1550"),
            ("SUP-06", "Tech Parts Warehouse","Dino", "Roa",   "09176667788", "dino@techparts.ph", "Warehouse 5, Pasig Industrial", "Pasig", "1600")
        };
        foreach (var s in supplierDefs)
        {
            suppliers.Add(new Supplier
            {
                SupplierCode = s.Code,
                SupplierName = s.Name,
                ContactFirstName = s.First,
                ContactLastName = s.Last,
                ContactNumber = s.Num,
                EmailAddress = s.Mail,
                Address = s.Street,
                City = s.City,
                StateOrProvince = "Metro Manila",
                PostalCode = s.Postal,
                Country = "Philippines",
                Notes = "Recommended supplier",
                IsActive = true,
                CreatedAt = now
            });
        }
        tenant.Suppliers.AddRange(suppliers);
        await tenant.SaveChangesAsync();

        var parts = new List<Part>();
        var partDefs = new (string Code, string Name, string Category, string Mfr, decimal Cost, decimal Price)[]
        {
            ("PRT-01", "Laptop Battery 6-Cell", "Battery", "OEM", 1450.00m, 2400.00m),
            ("PRT-02", "15.6\" HD LCD Screen", "Screen", "BOE", 2100.00m, 3400.00m),
            ("PRT-03", "Laptop Charger 65W", "Adapter", "OEM", 750.00m, 1400.00m),
            ("PRT-04", "SSD 256GB SATA III", "Storage", "Kingston", 1150.00m, 1850.00m),
            ("PRT-05", "SSD 512GB NVMe", "Storage", "Crucial", 1950.00m, 2750.00m),
            ("PRT-06", "RAM 8GB DDR4 SODIMM", "Memory", "Crucial", 1350.00m, 2100.00m),
            ("PRT-07", "RAM 16GB DDR4 SODIMM", "Memory", "Crucial", 2450.00m, 3600.00m),
            ("PRT-08", "Keyboard Replacement HP", "Keyboard", "OEM", 620.00m, 1150.00m),
            ("PRT-09", "Laptop Fan", "Cooling", "OEM", 380.00m, 750.00m),
            ("PRT-10", "Thermal Paste (tube)", "Cooling", "Arctic", 290.00m, 520.00m),
            ("PRT-11", "Motherboard Capacitor Kit", "Motherboard", "Assorted", 260.00m, 450.00m),
            ("PRT-12", "Wi-Fi Adapter USB", "Networking", "TP-Link", 420.00m, 800.00m),
            ("PRT-13", "External HDD 1TB", "Storage", "Seagate", 2350.00m, 3100.00m),
            ("PRT-14", "Display Cable", "Screen", "OEM", 180.00m, 350.00m),
            ("PRT-15", "DC Jack", "Motherboard", "OEM", 320.00m, 620.00m)
        };
        for (int i = 0; i < partDefs.Length; i++)
        {
            var p = partDefs[i];
            parts.Add(new Part
            {
                SupplierId = suppliers[i % suppliers.Count].SupplierId,
                PartCode = p.Code,
                PartName = p.Name,
                Category = p.Category,
                Manufacturer = p.Mfr,
                Model = p.Name,
                UnitCost = p.Cost,
                UnitPrice = p.Price,
                IsActive = true,
                CreatedAt = now
            });
        }
        tenant.Parts.AddRange(parts);
        await tenant.SaveChangesAsync();

        // ── Repair requests (240) over ~12 months ──
        var serviceModels = new[]
        {
            "Data Recovery", "Screen Replacement", "Laptop Repair", "Virus & Malware Removal",
            "Battery Replacement", "Motherboard Repair", "SSD Upgrade", "Keyboard Replacement",
            "Clean & Rebuild", "Network Setup", "Charging Port Repair", "Virus Removal",
            "Screen Repair", "Fan Cleaning", "Operating System Install"
        };
        var issues = new[]
        {
            "Unit won't boot; power light blinks.",
            "Screen very dim and has vertical lines.",
            "Battery drains in under an hour.",
            "Slow to a crawl with constant 100% disk usage.",
            "Blue screen with MEMORY_MANAGEMENT error.",
            "Laptop shuts off randomly when moved.",
            "Keyboard keys double-typing and stuck.",
            "No display after BIOS update.",
            "Charging port is loose; must hold cable at an angle.",
            "Frequent crashes during heavy load / gaming.",
            "Water-damage suspect; unit smelled burnt.",
            "Wi-Fi keeps disconnecting every few minutes.",
            "Virus popups and browser redirects.",
            "Fan noise is very loud and unit overheats.",
            "Data lost after failed OS update — needs recovery."
        };

        const int repairCount = 280;
        var repairs = new List<RepairRequest>();
        var payments = new List<Payment>();
        var paymentByRepair = new List<(Payment payment, RepairRequest rr)>();
        var statusHistories = new List<RepairStatusHistory>();
        var repairParts = new List<RepairPart>();

        int seq = 1;

        var customerIds = customers.Select(c => c.CustomerId).ToList();

        for (int i = 0; i < repairCount; i++)
        {
            int cid;
            var x = rng.NextDouble();
            // Heavy bias toward the first (VIP) customers.
            cid = customerIds[Math.Min((int)(Math.Pow(x, 3.0) * customerIds.Count), customerIds.Count - 1)];

            // Keep a handful of customers truly repair-free.
            while (neverPurchased.Contains(customers.FindIndex(c => c.CustomerId == cid)))
                cid = customerIds[Math.Min((int)(Math.Pow(rng.NextDouble(), 3.0) * customerIds.Count), customerIds.Count - 1)];

            var isInactive = inactiveIndexes.Contains(customers.FindIndex(c => c.CustomerId == cid));

            var requestDate = now.AddDays(-rng.Next(0, 365));
            if (isInactive)
            {
                // Force the last visit into the older window for re-engagement basis.
                requestDate = now.AddDays(-rng.Next(90, 200));
            }

            var model = serviceModels[rng.Next(serviceModels.Length)];
            var serial = $"SN-{rng.Next(10000, 99999)}";

            // Statuses: 72% completed, 8% pending, 6% approved, 5% in-progress,
            //           5% rejected, 4% reassigned.
            var statusRoll = rng.NextDouble();
            var status = statusRoll switch
            {
                < 0.72 => RepairStatus.Completed,
                < 0.80 => RepairStatus.Pending,
                < 0.86 => RepairStatus.Approved,
                < 0.91 => RepairStatus.InProgress,
                < 0.96 => RepairStatus.Rejected,
                _ => RepairStatus.Reassigned
            };

            var estimatedCost = model switch
            {
                "Data Recovery" => rng.Next(1200, 6000),
                "Screen Replacement" => rng.Next(2000, 5000),
                "Motherboard Repair" => rng.Next(1500, 8000),
                "Charging Port Repair" => rng.Next(800, 2200),
                "SSD Upgrade" => rng.Next(1800, 4800),
                _ => rng.Next(300, 2500)
            };

            var rr = new RepairRequest
            {
                RequestNumber = $"REQ-{requestDate:yyyyMMdd}-{seq:0000}",
                CustomerId = cid,
                DeviceId = rng.Next(0, 3) == 0 ? devices[rng.Next(devices.Count)].DeviceId : (int?)null,
                DeviceModel = model,
                SerialNumber = serial,
                IssueDescription = issues[rng.Next(issues.Length)],
                Priority = (Priority)rng.Next(0, 4),
                Status = status,
                EstimatedCost = estimatedCost,
                RequestDate = requestDate
            };

            if (status == RepairStatus.Completed)
            {
                rr.CompletionDate = requestDate.AddDays(rng.Next(1, 6));
                rr.ActualCost = estimatedCost + rng.Next(-150, 350);
                rr.PartsCost = rr.ActualCost * (1 + rng.Next(0, 4)) / 10m;
                rr.LaborCost = rr.ActualCost - rr.PartsCost!.Value;
                rr.TechnicianNotes = "Repaired and tested OK.";
                rr.AssignedToStaffId = "staff";

                // Payment (transaction) for nearly every completed repair.
                if (rng.NextDouble() < 0.97)
                {
                    var isPaid = rng.NextDouble() < 0.92;
                    var payment = new Payment
                    {
                        RepairRequestId = 0, // set below after SaveChanges
                        Amount = Math.Round(rr.ActualCost!.Value * (isPaid ? 1m : 0.60m), 2),
                        PaymentDate = rr.CompletionDate!.Value.AddHours(rng.Next(1, 72)),
                        PaymentMethod = new[] { "Cash", "G-Cash", "Card", "Bank" }[rng.Next(4)],
                        ReferenceNumber = $"REF-{rng.Next(1_000_000, 9_999_999)}",
                        IsPaid = isPaid,
                        IsVoid = rng.NextDouble() < 0.04
                    };
                    payments.Add(payment);
                    paymentByRepair.Add((payment, rr));
                }
            }
            else if ((status == RepairStatus.InProgress || status == RepairStatus.Approved) && rng.NextDouble() < 0.70)
            {
                // Deposit transaction for in-progress or approved diagnostic tickets
                var deposit = new Payment
                {
                    RepairRequestId = 0,
                    Amount = Math.Round(estimatedCost * 0.50m, 2),
                    PaymentDate = requestDate.AddHours(rng.Next(2, 24)),
                    PaymentMethod = new[] { "Cash", "G-Cash", "Card" }[rng.Next(3)],
                    ReferenceNumber = $"DEP-{rng.Next(1_000_000, 9_999_999)}",
                    IsPaid = true,
                    IsVoid = false
                };
                payments.Add(deposit);
                paymentByRepair.Add((deposit, rr));
                rr.AssignedToStaffId = "staff";
            }
            else
            {
                rr.AssignedToStaffId = status == RepairStatus.Reassigned ? "staff" : null;
            }

            repairs.Add(rr);
            seq++;

            statusHistories.Add(new RepairStatusHistory
            {
                RepairRequestId = 0, // set after save
                OldStatus = RepairStatus.Pending,
                NewStatus = status,
                ChangedByUserId = status == RepairStatus.Completed ? "staff" : "manager",
                ChangedAt = rr.RequestDate.AddDays(1),
                Notes = status == RepairStatus.Completed ? "Work completed." : $"Marked {status}."
            });
        }

        // ── Interactions (inquiries / complaints / feedback) (220) ──
        var interactions = new List<CustomerInteraction>();
        var interactionSubjects = new[]
        {
            "Inquiry: price quote for screen replacement",
            "Complaint: unit still slow after repair",
            "Feedback: great service, will recommend",
            "Inquiry: do you repair MacBooks?",
            "Complaint: charging port issue returned",
            "Feedback: pickup reminder is helpful",
            "Inquiry: data recovery for external drive",
            "Complaint: long waiting time for approval",
            "Feedback: technician explained everything clearly",
            "Inquiry: warranty on replaced battery"
        };

        var interactionNotes = new[]
        {
            "Customer contacted shop regarding diagnostic evaluation status. Advised unit is currently undergoing power rail measurement.",
            "Customer visited shop to check turnaround time. Technician confirmed parts arrival and target completion tomorrow.",
            "Customer requested detailed quotation breakdown for replacement display vs labor. Provided formal estimate.",
            "Client reported minor thermal throttling during video rendering. Recommended internal fan cleaning and thermal pad replacement.",
            "Customer asked about warranty coverage on logic board capacitor rework. Reassured with 90-day comprehensive guarantee.",
            "Customer requested data backup prior to SSD upgrade. Confirmed all personal files and browser profiles backed up safely.",
            "Client expressed high satisfaction with speedy turnaround and courteous technical assistance.",
            "Customer inquired about compatibility of upgrading to 32GB DDR4 memory for architectural CAD workstation.",
            "Client called regarding pickup authorization for family member. Recorded authorized representative details.",
            "Customer reported power adapter was misplaced. Matched compatible OEM 65W fast charger from inventory."
        };

        for (int i = 0; i < 220; i++)
        {
            var type = rng.Next(0, 3) switch { 0 => InteractionType.Inquiry, 1 => InteractionType.Complaint, _ => InteractionType.Feedback };
            var statusRoll = rng.NextDouble();
            var status = statusRoll switch
            {
                < 0.5 => InteractionStatus.Closed,
                < 0.85 => InteractionStatus.InProgress,
                _ => InteractionStatus.Open
            };

            var interaction = new CustomerInteraction
            {
                CustomerId = customers[rng.Next(customers.Count)].CustomerId,
                InteractionType = type,
                Status = status,
                Priority = (InteractionPriority)rng.Next(0, 3),
                Subject = interactionSubjects[rng.Next(interactionSubjects.Length)],
                Notes = interactionNotes[rng.Next(interactionNotes.Length)],
                InteractionByUserId = rng.Next(0, 2) == 0 ? "technician" : "admin",
                InteractionDate = now.AddDays(-rng.Next(0, 365)),
                IsActive = true
            };

            if (interaction.Status == InteractionStatus.Closed)
                interaction.ClosedAt = interaction.InteractionDate.AddDays(rng.Next(1, 5));

            interactions.Add(interaction);
        }

        // ── Follow-ups (200) ──
        var followUpSubjects = new[]
        {
            "Post-repair 48hr diagnostic check and temperature stability verification",
            "Quotation approval callback for logic board capacitor replacement",
            "Satisfaction inquiry: replacement IPS screen visual quality check",
            "Unclaimed unit notification — repair completed and ready for counter pickup",
            "Follow up: battery replacement cycle calibration and health report",
            "Preventative maintenance reminder: 6-month thermal fan de-dusting",
            "Software update confirmation: Windows 11 clean installation and driver check",
            "VIP corporate client workstation fleet diagnostic follow-up"
        };

        var followUpNotes = new[]
        {
            "Contacted customer via phone; confirmed system is running smoothly without thermal throttling.",
            "Called client regarding repair quote approval. Customer confirmed proceeding with service.",
            "Verified replacement display panel brightness and color gamut. Customer highly satisfied.",
            "Sent SMS alert confirming unit is securely packed at pickup counter ready for collection.",
            "Advised customer on best charging habits to prolong lifespan of new 6-cell battery.",
            "Customer scheduled on-site pickup for tomorrow afternoon.",
            "Confirmed all accounting and productivity software functioning properly after SSD migration.",
            "Discussed recurring annual preventive maintenance schedule with office administrator."
        };

        var followUps = new List<FollowUp>();
        for (int i = 0; i < 200; i++)
        {
            var scheduled = now.AddDays(-rng.Next(0, 60));
            var status = scheduled < now.AddDays(-30) ? FollowUpStatus.Cancelled : rng.NextDouble() < 0.4 ? FollowUpStatus.Completed : FollowUpStatus.Scheduled;

            followUps.Add(new FollowUp
            {
                CustomerId = customers[rng.Next(customers.Count)].CustomerId,
                Subject = followUpSubjects[rng.Next(followUpSubjects.Length)],
                Notes = followUpNotes[rng.Next(followUpNotes.Length)],
                ScheduledAt = scheduled,
                CompletedAt = status == FollowUpStatus.Completed ? scheduled.AddDays(1) : null,
                Channel = (FollowUpChannel)rng.Next(0, 4),
                Status = status,
                AssignedToUserId = "technician",
                CreatedAt = scheduled.AddDays(-1),
                IsActive = true
            });
        }

        // ── Persist repairs, then link payments / history / parts ──
        tenant.RepairRequests.AddRange(repairs);
        await tenant.SaveChangesAsync();

        var partsByIndex = parts;

        for (int i = 0; i < repairs.Count; i++)
        {
            var rr = repairs[i];

            statusHistories[i].RepairRequestId = rr.RepairRequestId;

            if (rr.Status == RepairStatus.Completed && rng.NextDouble() < 0.65)
            {
                var part = partsByIndex[rng.Next(partsByIndex.Count)];
                repairParts.Add(new RepairPart
                {
                    RepairRequestId = rr.RepairRequestId,
                    PartId = part.PartId,
                    QuantityUsed = rng.Next(1, 3),
                    UnitCostAtTime = part.UnitCost,
                    UnitPriceAtTime = part.UnitPrice,
                    UsedAt = rr.CompletionDate!.Value
                });
            }
        }

        // Payments need RepairRequestId — persisted repair ids are stable.
        foreach (var entry in paymentByRepair)
            entry.payment.RepairRequestId = entry.rr.RepairRequestId;

        tenant.Payments.AddRange(payments);
        tenant.RepairStatusHistories.AddRange(statusHistories);
        tenant.RepairParts.AddRange(repairParts);
        tenant.CustomerInteractions.AddRange(interactions);
        tenant.FollowUps.AddRange(followUps);

        // Loyalty points: roughly correlate with number of completed repairs.
        var enrichedIds = new HashSet<int>();
        foreach (var rr in repairs.Where(r => r.Status == RepairStatus.Completed))
        {
            enrichedIds.Add(rr.CustomerId);
        }
        foreach (var cust in customers.Where(c => enrichedIds.Contains(c.CustomerId)))
        {
            var count = repairs.Count(r => r.CustomerId == cust.CustomerId && r.Status == RepairStatus.Completed);
            cust.LoyaltyPoints = Math.Min(count, 30) * rng.Next(20, 60);
        }

        await tenant.SaveChangesAsync();

        // ── Loyalty membership (master DB, references tenant customers) ──
        var enrolled = new List<CustomerLoyaltyAccount>();
        foreach (var program in programs)
        {
            foreach (var cust in customers)
            {
                var completed = repairs.Count(r =>
                    r.CustomerId == cust.CustomerId && r.Status == RepairStatus.Completed);
                var spent = payments
                    .Where(p => p.IsPaid && !p.IsVoid &&
                                p.RepairRequestId != 0 &&
                                repairs.Any(r => r.RepairRequestId == p.RepairRequestId &&
                                                 r.CustomerId == cust.CustomerId))
                    .Sum(p => p.Amount);

                bool qualifies = program.ProgramName switch
                {
                    "Fixory Rewards Club" =>
                        completed >= 3 && spent >= 1500,
                    "VIP Service Club" =>
                        completed >= 6 && spent >= 10000,
                    "Monthly Visitor Boost" =>
                        completed >= 2 && spent >= 500,
                    _ => false
                };

                if (qualifies && rng.NextDouble() < 0.40)
                {
                    enrolled.Add(new CustomerLoyaltyAccount
                    {
                        CustomerId = cust.CustomerId,
                        LoyaltyProgramId = program.LoyaltyProgramId,
                        Points = Math.Max(50, cust.LoyaltyPoints ?? 0),
                        TotalSpent = spent,
                        JoinedDate = cust.CreatedAt.AddDays(rng.Next(5, 60)),
                        IsActive = rng.NextDouble() > 0.08
                    });
                }
            }
        }

        if (enrolled.Count > 0)
        {
            var existingPairs = (await master.CustomerLoyaltyAccounts
                .Select(a => new { a.CustomerId, a.LoyaltyProgramId })
                .ToListAsync())
                .Select(x => $"{x.CustomerId}_{x.LoyaltyProgramId}")
                .ToHashSet();

            var newAccounts = enrolled
                .Where(a => !existingPairs.Contains($"{a.CustomerId}_{a.LoyaltyProgramId}"))
                .ToList();

            if (newAccounts.Count > 0)
            {
                master.CustomerLoyaltyAccounts.AddRange(newAccounts);
                await master.SaveChangesAsync();
            }
        }
    }

    private static async Task SeedRetentionDefaultsAsync(TenantCrmDbContext tenant)
    {
        // ── 1. Settings ──
        if (!await tenant.RetentionSettings.AnyAsync())
        {
            tenant.RetentionSettings.Add(new RetentionSettings
            {
                InactiveThresholdDays = 180,
                AtRiskThresholdDays = 90,
                AntiFatigueDays = 14,
                DefaultOfferValidityDays = 14,
                SmtpHost = "localhost",
                SmtpPort = 25,
                SmtpFromEmail = "retention@fixorycrm.local",
                SmtpFromName = "Fixory Computer Repair Services",
                SmtpEnableSsl = false,
                UpdatedAt = DateTime.UtcNow
            });
            await tenant.SaveChangesAsync();
        }

        // ── 2. Templates ──
        if (!await tenant.RetentionEmailTemplates.AnyAsync())
        {
            tenant.RetentionEmailTemplates.AddRange(
                new RetentionEmailTemplate
                {
                    Segment = RetentionSegment.New,
                    TemplateName = "New Customer — Welcome & 5% Next Service",
                    Subject = "Thank You for Choosing Fixory — Enjoy 5% Off Your Next Computer Service",
                    Body = "<p>Thank you for trusting Fixory Computer Repair Services with your device!</p><p>As our way of saying thank you, please enjoy <strong>{{discount_percent}} off</strong> your next maintenance, checkup, or hardware accessory.</p><p>Use promo code <strong>{{promo_code}}</strong> within the next {{validity_days}} days (expires {{expiration_date}}).</p>",
                    DefaultDiscountPercent = 5m,
                    ValidityDays = 14,
                    IsActive = true
                },
                new RetentionEmailTemplate
                {
                    Segment = RetentionSegment.Returning,
                    TemplateName = "Returning Customer — Device Health & Upgrade Check",
                    Subject = "Got Another Device Needing Care? Save with Fixory",
                    Body = "<p>We hope your recently repaired computer is running at peak speed!</p><p>Whether you have a second computer that needs a tune-up or are looking for a hardware upgrade, we are pleased to offer you <strong>{{discount_percent}} off</strong> labor.</p><p>Mention promo code <strong>{{promo_code}}</strong> upon arrival. Valid through {{expiration_date}}.</p>",
                    DefaultDiscountPercent = 10m,
                    ValidityDays = 14,
                    IsActive = true
                },
                new RetentionEmailTemplate
                {
                    Segment = RetentionSegment.Loyal,
                    TemplateName = "Loyal Customer — VIP Appreciation Reward",
                    Subject = "Exclusive Fixory VIP Reward — 10% Off Your Next Repair",
                    Body = "<p>Thank you for being one of Fixory's most valued customers!</p><p>As a member of our loyal client community, we have activated a special <strong>{{discount_percent}} VIP discount</strong> with priority diagnostic bench placement.</p><p>Promo Code: <strong>{{promo_code}}</strong> &bull; Valid through {{expiration_date}}.</p>",
                    DefaultDiscountPercent = 10m,
                    ValidityDays = 14,
                    IsActive = true
                },
                new RetentionEmailTemplate
                {
                    Segment = RetentionSegment.AtRisk,
                    TemplateName = "At Risk — Preventative Care & Tune-up",
                    Subject = "Is Your Computer Running Slower? Time for a Fixory Tune-up (10% Off)",
                    Body = "<p>It has been a few months since your last repair, and computers accumulate dust, thermal paste wear, and software clutter over time.</p><p>Bring your system in for a preventative clean-up and tune-up and get <strong>{{discount_percent}} off</strong> with code <strong>{{promo_code}}</strong>.</p><p>Offer valid through {{expiration_date}}.</p>",
                    DefaultDiscountPercent = 10m,
                    ValidityDays = 14,
                    IsActive = true
                },
                new RetentionEmailTemplate
                {
                    Segment = RetentionSegment.Inactive,
                    TemplateName = "Inactive — Re-engagement & Comprehensive Diagnostic",
                    Subject = "We Miss You at Fixory! Here is 15% Off Your Next Computer Repair",
                    Body = "<p>It has been over 6 months since we last serviced your system, and we want to ensure everything is operating reliably!</p><p>We would love to welcome you back with a comprehensive diagnostic and <strong>{{discount_percent}} off</strong> all labor and services.</p><p>Redeem with code <strong>{{promo_code}}</strong> before {{expiration_date}}.</p>",
                    DefaultDiscountPercent = 15m,
                    ValidityDays = 14,
                    IsActive = true
                }
            );
            await tenant.SaveChangesAsync();
        }

        // ── 3. Sample Requests (if none exist) ──
        if (!await tenant.RetentionRequests.AnyAsync())
        {
            var customers = await tenant.Customers.Take(10).ToListAsync();
            if (customers.Count >= 3)
            {
                var req1 = new RetentionRequest
                {
                    CustomerId = customers[0].CustomerId,
                    TargetSegment = RetentionSegment.AtRisk,
                    ActionType = "Discount",
                    ProposedDiscountPercent = 10m,
                    RetentionDetails = "Customer had GPU fan replacement 4 months ago. Recommend 6-month thermal maintenance before summer heat.",
                    ReasonCategory = "Prevent Customer Churn",
                    ReasonNote = "Customer indicated high gaming workload; preventative outreach prevents hardware failure.",
                    Status = RetentionRequestStatus.Pending,
                    SubmittedByUserId = "manager",
                    SubmittedByName = "Manager User",
                    SubmittedAt = DateTime.UtcNow.AddDays(-2),
                    AddedToCampaign = false
                };

                var req2 = new RetentionRequest
                {
                    CustomerId = customers[1].CustomerId,
                    TargetSegment = RetentionSegment.Loyal,
                    ActionType = "Discount",
                    ProposedDiscountPercent = 15m,
                    RetentionDetails = "High-value recurring client with 4 completed repairs. VIP loyalty discount for next office computer upgrade.",
                    ReasonCategory = "Increase Customer Lifetime Value",
                    ReasonNote = "Strategic account with multiple workstations.",
                    Status = RetentionRequestStatus.Approved,
                    SubmittedByUserId = "manager",
                    SubmittedByName = "Manager User",
                    SubmittedAt = DateTime.UtcNow.AddDays(-5),
                    ReviewedByUserId = "admin",
                    ReviewedByName = "Admin User",
                    ReviewedAt = DateTime.UtcNow.AddDays(-4),
                    ReviewRemarks = "Approved as part of Q3 VIP account retention push.",
                    AddedToCampaign = true,
                    CampaignAddedAt = DateTime.UtcNow.AddDays(-4)
                };

                var log2 = new RetentionEmailLog
                {
                    RetentionRequest = req2,
                    CustomerId = customers[1].CustomerId,
                    RecipientName = $"{customers[1].FirstName} {customers[1].LastName}".Trim(),
                    RecipientEmail = customers[1].Email ?? "client@fixorycrm.local",
                    Subject = "Exclusive Fixory VIP Reward — 15% Off Your Next Workstation Upgrade",
                    FormattedBody = "<p>Dear <strong>" + customers[1].FirstName + "</strong>,</p><p>As one of our VIP clients, enjoy <strong>15% OFF</strong> on your next repair or workstation maintenance service.</p><p>Promo Code: <strong>FIXORY-VIP-7821</strong> (Valid for 14 days).</p>",
                    Segment = RetentionSegment.Loyal,
                    DiscountPercent = 15m,
                    PromoCode = "FIXORY-VIP-7821",
                    ValidUntil = DateTime.UtcNow.AddDays(10),
                    IsDispatched = true,
                    DispatchedAt = DateTime.UtcNow.AddDays(-4),
                    IsAutomated = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-4),
                    DeliveryStatus = "Delivered"
                };

                var req3 = new RetentionRequest
                {
                    CustomerId = customers[2].CustomerId,
                    TargetSegment = RetentionSegment.Inactive,
                    ActionType = "Discount",
                    ProposedDiscountPercent = 25m,
                    RetentionDetails = "Proposing a 25% discount to win back client who hasn't visited in 9 months.",
                    ReasonCategory = "Promotional or Strategic Decision",
                    ReasonNote = "High discount proposed to test win-back response.",
                    Status = RetentionRequestStatus.Rejected,
                    SubmittedByUserId = "manager",
                    SubmittedByName = "Manager User",
                    SubmittedAt = DateTime.UtcNow.AddDays(-7),
                    ReviewedByUserId = "admin",
                    ReviewedByName = "Admin User",
                    ReviewedAt = DateTime.UtcNow.AddDays(-6),
                    RejectionReason = "Discount exceeds company policy limit of 15% for inactive win-back campaigns.",
                    ReviewRemarks = "Please adjust discount to 15% and resubmit.",
                    AddedToCampaign = false
                };

                var requests = new List<RetentionRequest> { req1, req2, req3 };
                var logs = new List<RetentionEmailLog> { log2 };

                if (customers.Count >= 6)
                {
                    var req4 = new RetentionRequest
                    {
                        CustomerId = customers[3].CustomerId,
                        TargetSegment = RetentionSegment.Returning,
                        ActionType = "Discount",
                        ProposedDiscountPercent = 10m,
                        RetentionDetails = "Regular customer due for periodic laptop thermal paste and dust cleanout service.",
                        ReasonCategory = "Increase Customer Lifetime Value",
                        ReasonNote = "Frequent repair history with high satisfaction rating.",
                        Status = RetentionRequestStatus.Approved,
                        SubmittedByUserId = "manager",
                        SubmittedByName = "Manager User",
                        SubmittedAt = DateTime.UtcNow.AddDays(-3),
                        ReviewedByUserId = "admin",
                        ReviewedByName = "Admin User",
                        ReviewedAt = DateTime.UtcNow.AddDays(-2),
                        ReviewRemarks = "Standard 10% preventative maintenance promotion approved.",
                        AddedToCampaign = true,
                        CampaignAddedAt = DateTime.UtcNow.AddDays(-2)
                    };

                    var log4 = new RetentionEmailLog
                    {
                        RetentionRequest = req4,
                        CustomerId = customers[3].CustomerId,
                        RecipientName = $"{customers[3].FirstName} {customers[3].LastName}".Trim(),
                        RecipientEmail = customers[3].Email ?? "customer3@fixorycrm.local",
                        Subject = "Keep Your Laptop Running Cool — 10% Off Tune-up",
                        FormattedBody = "<p>Dear " + customers[3].FirstName + ",</p><p>Bring in your computer for a quick maintenance checkup and receive <strong>10% off</strong> using code <strong>COOL-TUNE-10</strong>.</p>",
                        Segment = RetentionSegment.Returning,
                        DiscountPercent = 10m,
                        PromoCode = "COOL-TUNE-10",
                        ValidUntil = DateTime.UtcNow.AddDays(12),
                        IsDispatched = true,
                        DispatchedAt = DateTime.UtcNow.AddDays(-2),
                        IsAutomated = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-2),
                        DeliveryStatus = "Sent"
                    };

                    var req5 = new RetentionRequest
                    {
                        CustomerId = customers[4].CustomerId,
                        TargetSegment = RetentionSegment.New,
                        ActionType = "Discount",
                        ProposedDiscountPercent = 5m,
                        RetentionDetails = "First-time visitor welcome discount voucher for accessory purchases or future service.",
                        ReasonCategory = "Build Customer Loyalty",
                        ReasonNote = "Welcome onboarding package.",
                        Status = RetentionRequestStatus.Pending,
                        SubmittedByUserId = "staff",
                        SubmittedByName = "Staff User",
                        SubmittedAt = DateTime.UtcNow.AddDays(-1),
                        AddedToCampaign = false
                    };

                    var req6 = new RetentionRequest
                    {
                        CustomerId = customers[5].CustomerId,
                        TargetSegment = RetentionSegment.Inactive,
                        ActionType = "Discount",
                        ProposedDiscountPercent = 15m,
                        RetentionDetails = "Lapsed client from last quarter. Offer comprehensive diagnostic to re-engage.",
                        ReasonCategory = "Prevent Customer Churn",
                        ReasonNote = "High previous spend, win-back opportunity.",
                        Status = RetentionRequestStatus.Approved,
                        SubmittedByUserId = "manager",
                        SubmittedByName = "Manager User",
                        SubmittedAt = DateTime.UtcNow.AddDays(-1),
                        ReviewedByUserId = "admin",
                        ReviewedByName = "Admin User",
                        ReviewedAt = DateTime.UtcNow,
                        ReviewRemarks = "Approved for automatic dispatch.",
                        AddedToCampaign = true,
                        CampaignAddedAt = DateTime.UtcNow
                    };

                    var log6 = new RetentionEmailLog
                    {
                        RetentionRequest = req6,
                        CustomerId = customers[5].CustomerId,
                        RecipientName = $"{customers[5].FirstName} {customers[5].LastName}".Trim(),
                        RecipientEmail = customers[5].Email ?? "customer5@fixorycrm.local",
                        Subject = "We Miss You at Fixory — 15% Off Your Next Computer Repair",
                        FormattedBody = "<p>Dear " + customers[5].FirstName + ",</p><p>We would love to see you back. Enjoy <strong>15% off</strong> with code <strong>COMEBACK-15</strong>.</p>",
                        Segment = RetentionSegment.Inactive,
                        DiscountPercent = 15m,
                        PromoCode = "COMEBACK-15",
                        ValidUntil = DateTime.UtcNow.AddDays(14),
                        IsDispatched = false,
                        IsAutomated = true,
                        CreatedAt = DateTime.UtcNow,
                        DeliveryStatus = "Pending"
                    };

                    requests.AddRange(new[] { req4, req5, req6 });
                    logs.AddRange(new[] { log4, log6 });
                }

                tenant.RetentionRequests.AddRange(requests);
                tenant.RetentionEmailLogs.AddRange(logs);
                await tenant.SaveChangesAsync();
            }
        }
    }

    private static async Task SeedMissingLoyaltyAccountsAsync(
        MasterCrmDbContext master,
        ITenantDbContextFactory factory,
        List<LoyaltyProgram> programs,
        List<CompanyDatabase> tenantDatabases,
        ILogger logger)
    {
        if (programs.Count == 0) return;

        var existingPairs = (await master.CustomerLoyaltyAccounts
            .Select(a => new { a.CustomerId, a.LoyaltyProgramId })
            .ToListAsync())
            .Select(x => $"{x.CustomerId}_{x.LoyaltyProgramId}")
            .ToHashSet();

        var toAdd = new List<CustomerLoyaltyAccount>();
        var rng = new Random(42);

        foreach (var tdb in tenantDatabases)
        {
            try
            {
                await using var tenant = await factory.CreateAsync(tdb.CompanyId);
                var customers = await tenant.Customers.AsNoTracking().ToListAsync();
                if (customers.Count == 0) continue;

                var repairs = await tenant.RepairRequests.AsNoTracking().ToListAsync();
                var payments = await tenant.Payments.AsNoTracking()
                    .Where(p => p.IsPaid && !p.IsVoid)
                    .ToListAsync();

                var companyPrograms = programs.Where(p => p.CompanyId == tdb.CompanyId).ToList();

                foreach (var program in companyPrograms)
                {
                    foreach (var cust in customers)
                    {
                        if (existingPairs.Contains($"{cust.CustomerId}_{program.LoyaltyProgramId}"))
                            continue;

                        var completed = repairs.Count(r =>
                            r.CustomerId == cust.CustomerId && r.Status == RepairStatus.Completed);
                        var spent = payments
                            .Where(p => p.RepairRequestId != 0 &&
                                        repairs.Any(r => r.RepairRequestId == p.RepairRequestId &&
                                                         r.CustomerId == cust.CustomerId))
                            .Sum(p => p.Amount);

                        bool qualifies = program.ProgramName switch
                        {
                            "Fixory Rewards Club" => completed >= 2 && spent >= 1000,
                            "VIP Service Club" => completed >= 4 && spent >= 5000,
                            "Monthly Visitor Boost" => completed >= 1 && spent >= 300,
                            _ => completed >= 1
                        };

                        if (qualifies && rng.NextDouble() < 0.40)
                        {
                            var account = new CustomerLoyaltyAccount
                            {
                                CustomerId = cust.CustomerId,
                                LoyaltyProgramId = program.LoyaltyProgramId,
                                Points = Math.Max(50, cust.LoyaltyPoints ?? (completed * 40)),
                                TotalSpent = spent,
                                JoinedDate = cust.CreatedAt.AddDays(rng.Next(5, 60)),
                                IsActive = true
                            };
                            toAdd.Add(account);
                            existingPairs.Add($"{cust.CustomerId}_{program.LoyaltyProgramId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not seed loyalty accounts for company {CompanyId}", tdb.CompanyId);
            }
        }

        if (toAdd.Count > 0)
        {
            master.CustomerLoyaltyAccounts.AddRange(toAdd);
            await master.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} loyalty memberships into master database.", toAdd.Count);
        }
    }
}