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
    public static async Task SeedAsync(IServiceProvider sp, ILogger<Program> logger)
    {
        var master = sp.GetRequiredService<MasterCrmDbContext>();

        // ── Migrations (both databases) ──
        logger.LogInformation("Applying master database migrations...");
        await master.Database.MigrateAsync();

        // ── Identity roles + demo users ──
        var users = sp.GetRequiredService<UserManager<User>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        await SeedIdentityAsync(users, roles);

        // ── Demo company + tenant database row ──
        await SeedCompanyAsync(master);

        // ── Loyalty programs, terms, subscription ──
        var seededPrograms = await SeedLoyaltyProgramsAsync(master);

        // ── Tenant database (company 1) ──
        var factory = sp.GetRequiredService<ITenantDbContextFactory>();
        await using var tenant = await factory.CreateAsync(1);

        logger.LogInformation("Applying tenant database migrations...");
        await tenant.Database.MigrateAsync();

        if (await tenant.Customers.AnyAsync())
        {
            logger.LogInformation("Tenant data already present — ensuring retention defaults are seeded...");
            await SeedRetentionDefaultsAsync(tenant);
            return;
        }

        logger.LogInformation("Seeding tenant demo data (80 customers, 240 repairs, 220+ transactions)...");
        await SeedTenantAsync(tenant, master, seededPrograms);
        await SeedRetentionDefaultsAsync(tenant);

        logger.LogInformation("Database seeding completed successfully.");
    }

    // ═══════════════════════ Identity ═══════════════════════

    private static async Task SeedIdentityAsync(
        UserManager<User> users, RoleManager<IdentityRole> roles)
    {
        var demoUsers = new[]
        {
            new { UserName = "superadmin", Email = "admin@fixory.local",      Password = "SuperAdmin@123", First = "Super",  Last = "Admin",    Role = "Super Admin" },
            new { UserName = "admin",      Email = "admin.user@fixory.local", Password = "Admin@123",      First = "Admin",  Last = "User",     Role = "Admin" },
            new { UserName = "manager",    Email = "manager@fixory.local",    Password = "Manager@123",    First = "Manager",Last = "User",     Role = "Manager" },
            new { UserName = "staff",      Email = "staff@fixory.local",      Password = "Staff@123",      First = "Juan",   Last = "Dela Cruz",Role = "Staff" }
        };

        foreach (var d in demoUsers)
        {
            if (!await roles.RoleExistsAsync(d.Role))
                await roles.CreateAsync(new IdentityRole(d.Role));

            if (await users.FindByNameAsync(d.UserName) != null)
                continue;

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

            var result = await users.CreateAsync(user, d.Password);
            if (result.Succeeded)
                await users.AddToRoleAsync(user, d.Role);
        }
    }

    // ═══════════════════════ Master ═══════════════════════

    private static async Task SeedCompanyAsync(MasterCrmDbContext master)
    {
        const string code = "FIXORY-001";

        var company = await master.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == 1 || c.CompanyCode == code);

        if (company is null)
        {
            company = new Company
            {
                CompanyCode = code,
                CompanyName = "Fixory Computer Repair Services",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            master.Companies.Add(company);
            await master.SaveChangesAsync();
        }

        var hasDb = await master.CompanyDatabases
            .AnyAsync(d => d.CompanyId == company.CompanyId);

        if (!hasDb)
        {
            master.CompanyDatabases.Add(new CompanyDatabase
            {
                CompanyId = company.CompanyId,
                ServerName = @"(localdb)\MSSQLLocalDB",
                DatabaseName = "DB_TenantRepairs_Company1",
                CredentialKey = "",
                IsActive = true
            });
            await master.SaveChangesAsync();
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
            await master.SaveChangesAsync();
        }

        if (!await master.Subscriptions.AnyAsync(s =>
                s.SubscriptionName == "Fixory Pro" && s.CompanyId == company.CompanyId))
        {
            master.Subscriptions.Add(new Subscription
            {
                CompanyId = company.CompanyId,
                SubscriptionName = "Fixory Pro",
                PricePerMonth = 1499.00m,
                MaxUsers = 25,
                MaxDevices = 500,
                StartDate = DateTime.UtcNow.AddDays(-365),
                EndDate = DateTime.UtcNow.AddDays(365),
                IsActive = true,
                BillingCycle = "Monthly"
            });
            await master.SaveChangesAsync();
        }
    }

    private static async Task<List<LoyaltyProgram>> SeedLoyaltyProgramsAsync(MasterCrmDbContext master)
    {
        var programs = new List<LoyaltyProgram>();

        if (!await master.LoyaltyPrograms.AnyAsync(p => p.ProgramName == "Fixory Rewards Club"))
        {
            var p = new LoyaltyProgram
            {
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

        if (!await master.LoyaltyPrograms.AnyAsync(p => p.ProgramName == "VIP Service Club"))
        {
            var p = new LoyaltyProgram
            {
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

        if (!await master.LoyaltyPrograms.AnyAsync(p => p.ProgramName == "Monthly Visitor Boost"))
        {
            var p = new LoyaltyProgram
            {
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

        await master.SaveChangesAsync();

        // Reload seeded program ids (fresh or existing).
        var names = programs.Select(p => p.ProgramName).ToList();
        if (names.Count == 0)
        {
            names = new List<string>
            {
                "Fixory Rewards Club", "VIP Service Club", "Monthly Visitor Boost"
            };
        }

        return await master.LoyaltyPrograms.AsNoTracking()
            .Where(p => names.Contains(p.ProgramName))
            .ToListAsync();
    }

    // ═══════════════════════ Tenant ═══════════════════════

    private static async Task SeedTenantAsync(
        TenantCrmDbContext tenant,
        MasterCrmDbContext master,
        List<LoyaltyProgram> programs)
    {
        var rng = new Random(2026);
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
            ("DEV-CB-01", "Razer Blade 15",              "Laptop",  "Razer", "Blade 15 Advanced")
        };

        foreach (var d in deviceDefs)
        {
            devices.Add(new Device
            {
                CompanyId = 1,
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

        // ── Customers (80) ──
        var firstNames = new[]
        {
            "Maria", "Jose", "Juan", "Ana", "Pedro", "Liza", "Carlo", "Rosa", "Miguel", "Elena",
            "Marco", "Sofia", "Danny", "Grace", "Ian", "Joyce", "Kevin", "Lorna", "Manny", "Nina",
            "Oscar", "Paula", "Ramon", "Sandra", "Tony", "Ursula", "Victor", "Wendy", "Xander", "Yolly",
            "Zandro", "Alma", "Berto", "Christine", "Dindo", "Erica", "Ferdie", "Gina", "Hector", "Isabel",
            "Jasper", "Kristine", "Leo", "Mila", "Nonoy", "Olive", "Perry", "Queen", "Rico", "Sheryl",
            "Tim", "Vina", "Willie", "Yanie", "Zed", "Aileen", "Brando", "Connie", "Dante", "Fely",
            "Gilbert", "Harold", "Imelda", "Jun", "Kayla", "Louie", "May", "Nestor", "Odessa", "Paolo",
            "Rafael", "Sonia", "Teresa", "Vince", "Winston", "Yumi", "Amon", "Bella", "Cesar", "Divina"
        };

        var lastNames = new[]
        {
            "Santos", "Reyes", "Cruz", "Bautista", "Ocampo", "Dela Cruz", "Garcia", "Mendoza",
            "Torres", "Flores", "Ramos", "Aquino", "Domingo", "Rosario", "Villanueva", "Navarro",
            "Salazar", "Perez", "Castillo", "Lopez", "Fernandez", "Gonzales", "Rivera", "Castro",
            "Valdez", "Aguilar", "Morales", "Alvarez", "Rojas", "Del Rosario", "Sison", "Tan",
            "Lim", "Chua", "Co", "Uy", "Sy", "Dizon", "Espinoza", "Marquez"
        };

        var customers = new List<Customer>();
        for (int i = 0; i < 80; i++)
        {
            var joined = now;
            var roll = i % 100;
            if (roll < 8) joined = now.AddDays(-rng.Next(2, 28));                    // 8% new this month
            else if (roll < 40) joined = now.AddDays(-rng.Next(30, 180));
            else if (roll < 70) joined = now.AddDays(-rng.Next(180, 365));
            else joined = now.AddDays(-rng.Next(365, 700));

            customers.Add(new Customer
            {
                FirstName = firstNames[i],
                LastName = lastNames[rng.Next(lastNames.Length)],
                Email = $"cust{i + 1:D2}@example.com",
                Phone = $"09{rng.Next(10_000_000, 99_999_999):D8}",
                Address =
                    $"{rng.Next(12, 499)} {new[] { "Pearl", "Rizal", "Bonifacio", "Quezon", "Luna", "Mahogany" }[rng.Next(6)]} St., " +
                    new[] { "Makati", "Manila", "Pasig", "Quezon City", "Taguig", "Mandaluyong" }[rng.Next(6)],
                LoyaltyPoints = 0,
                IsActive = true,
                CreatedAt = joined
            });
        }

        // 4 customers intentionally have zero repairs — for the "never purchased" segment.
        var neverPurchased = new[] { 3, 21, 44, 67 };
        var inactiveIndexes = Enumerable.Range(0, 80).Where(i => !neverPurchased.Contains(i) && i % 13 == 0).ToList();

        tenant.Customers.AddRange(customers);
        await tenant.SaveChangesAsync();

        // ── Suppliers + parts ──
        var suppliers = new List<Supplier>();
        var supplierDefs = new (string Code, string Name, string Person, string Num, string Mail)[]
        {
            ("SUP-01", "LaptopParts PH",   "Andres Lim",  "09171234567", "sales@laptopparts.ph"),
            ("SUP-02", "BatteryPro Supply", "Marlon Uy",  "09182223344", "orders@batterypro.ph"),
            ("SUP-03", "ScreenFix Distributor", "Grace Co","09175556677", "gc@screenfix.ph"),
            ("SUP-04", "PCHub Retail",      "Ben Torres", "09178889900", "ben@pchub.ph"),
            ("SUP-05", "SSD Mart",          "Cathy Tan",  "09173334455", "sales@ssdmart.ph"),
            ("SUP-06", "Tech Parts Warehouse","Dino Roa", "09176667788", "dino@techparts.ph")
        };
        foreach (var s in supplierDefs)
        {
            suppliers.Add(new Supplier
            {
                SupplierCode = s.Code,
                SupplierName = s.Name,
                ContactPerson = s.Person,
                ContactNumber = s.Num,
                EmailAddress = s.Mail,
                Address = $"{s.Code} Warehouse, Lagro, Quezon City",
                Notes = "Recommended supplier",
                IsActive = true,
                CreatedAt = now
            });
        }
        tenant.Suppliers.AddRange(suppliers);

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

        const int repairCount = 240;
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

        // ── Interactions (inquiries / complaints / feedback) ──
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

        for (int i = 0; i < 130; i++)
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
                Notes = "Auto-generated demo interaction used to exercise the retention engine.",
                InteractionByUserId = rng.Next(0, 2) == 0 ? "staff" : "manager",
                InteractionDate = now.AddDays(-rng.Next(0, 365)),
                IsActive = true
            };

            if (interaction.Status == InteractionStatus.Closed)
                interaction.ClosedAt = interaction.InteractionDate.AddDays(rng.Next(1, 5));

            interactions.Add(interaction);
        }

        // ── Follow-ups ──
        var followUps = new List<FollowUp>();
        for (int i = 0; i < 18; i++)
        {
            var scheduled = now.AddDays(-rng.Next(0, 45));
            var status = scheduled < now.AddDays(-30) ? FollowUpStatus.Cancelled : rng.NextDouble() < 0.3 ? FollowUpStatus.Completed : FollowUpStatus.Scheduled;

            followUps.Add(new FollowUp
            {
                CustomerId = customers[rng.Next(customers.Count)].CustomerId,
                Subject = "Follow up: repair status check",
                Notes = "Auto-generated follow-up from the demo seed.",
                ScheduledAt = scheduled,
                CompletedAt = status == FollowUpStatus.Completed ? scheduled.AddDays(1) : null,
                Channel = (FollowUpChannel)rng.Next(0, 4),
                Status = status,
                AssignedToUserId = "staff",
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

                if (qualifies && rng.NextDouble() < 0.15)
                {
                    enrolled.Add(new CustomerLoyaltyAccount
                    {
                        CustomerId = cust.CustomerId,
                        LoyaltyProgramId = program.LoyaltyProgramId,
                        Points = Math.Max(50, cust.LoyaltyPoints ?? 0),
                        TotalSpent = spent,
                        JoinedDate = cust.CreatedAt.AddDays(rng.Next(5, 60)),
                        IsActive = rng.NextDouble() > 0.1
                    });
                }
            }
        }

        if (enrolled.Count > 0)
        {
            var existingAccountIds = await master.CustomerLoyaltyAccounts
                .Select(a => a.CustomerLoyaltyAccountId)
                .ToListAsync();
            var newAccounts = enrolled.Where(a => !existingAccountIds.Contains(a.CustomerLoyaltyAccountId)).ToList();
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
            var customers = await tenant.Customers.Take(4).ToListAsync();
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
                    IsDispatched = false,
                    IsAutomated = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-4),
                    DeliveryStatus = "Pending"
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

                tenant.RetentionRequests.AddRange(req1, req2, req3);
                tenant.RetentionEmailLogs.Add(log2);
                await tenant.SaveChangesAsync();
            }
        }
    }
}