using System.Collections.Concurrent;
using System.Text.Json;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRM_ComputerRepair.infrastructure.Services;

public class CloudSyncService : ICloudSyncService
{
    private readonly IConfiguration _config;
    private readonly ILogger<CloudSyncService> _logger;
    private readonly ITenantDbContextFactory _localContextFactory;

    private static volatile bool _isCloudOnline = false;
    private static DateTime? _lastSyncTimeUtc = null;
    private static string? _lastError = null;
    private static readonly ConcurrentDictionary<int, string> _resolvedTenantConnStrs = new();
    private static readonly ConcurrentDictionary<int, bool> _migratedTenants = new();
    private static readonly SemaphoreSlim _syncLock = new(1, 1);

    public bool IsCloudOnline => _isCloudOnline;

    public CloudSyncService(
        IConfiguration config,
        ILogger<CloudSyncService> logger,
        ITenantDbContextFactory localContextFactory)
    {
        _config = config;
        _logger = logger;
        _localContextFactory = localContextFactory;
    }

    private IEnumerable<string> GetCandidateConnectionStrings(int companyId)
    {
        var list = new List<string>();

        // 1. From Configuration if present
        var tenantSection = _config.GetSection($"CloudSync:Tenants:{companyId}");
        var configured = tenantSection["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(configured))
            list.Add(configured);

        var server = tenantSection["Server"] ?? _config["CloudSync:CloudServer"] ?? "5.9.179.199,1433";
        var db = tenantSection["Database"];
        var user = tenantSection["UserId"];
        var pass = tenantSection["Password"];

        if (!string.IsNullOrWhiteSpace(db) && !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
        {
            list.Add($"Server={server};Database={db};User Id={user};Password={pass};Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
            list.Add($"Server=db70848.public.databaseasp.net,1433;Database={db};User Id={user};Password={pass};Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
            list.Add($"Server={db}.databaseasp.net,1433;Database={db};User Id={user};Password={pass};Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
        }

        // 2. Default MonsterASP configurations for known tenant companies
        switch (companyId)
        {
            case 1: // Fixtech
                list.Add("Server=5.9.179.199,1433;Database=db70863;User Id=db70863;Password=8a@H-Pi2Xw9?;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70848.public.databaseasp.net,1433;Database=db70863;User Id=db70863;Password=8a@H-Pi2Xw9?;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70863.databaseasp.net,1433;Database=db70863;User Id=db70863;Password=8a@H-Pi2Xw9?;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                break;
            case 2: // Bytecare
                list.Add("Server=5.9.179.199,1433;Database=db70865;User Id=db70865;Password=3t?YB+2n#7sE;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70848.public.databaseasp.net,1433;Database=db70865;User Id=db70865;Password=3t?YB+2n#7sE;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70865.databaseasp.net,1433;Database=db70865;User Id=db70865;Password=3t?YB+2n#7sE;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                break;
            case 3: // Techrevive
                list.Add("Server=5.9.179.199,1433;Database=db70866;User Id=db70866;Password=2Dy!W+4z?mK5;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70848.public.databaseasp.net,1433;Database=db70866;User Id=db70866;Password=2Dy!W+4z?mK5;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70866.databaseasp.net,1433;Database=db70866;User Id=db70866;Password=2Dy!W+4z?mK5;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                break;
            default: // MasterCRM / Default
                list.Add("Server=5.9.179.199,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70848.public.databaseasp.net,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                list.Add("Server=db70848.databaseasp.net,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=5;");
                break;
        }

        return list.Distinct();
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken ct = default)
    {
        // Check connectivity by probing Master database or company 1
        var testStrings = new[]
        {
            "Server=5.9.179.199,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=3;",
            "Server=db70848.public.databaseasp.net,1433;Database=db70848;User Id=db70848;Password=L!e48E=no+6Y;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=3;"
        };

        foreach (var cs in testStrings)
        {
            if (await TestSingleConnectionAsync(cs, ct))
            {
                _isCloudOnline = true;
                _lastError = null;
                return true;
            }
        }

        _isCloudOnline = false;
        return false;
    }

    private async Task<bool> TestSingleConnectionAsync(string connStr, CancellationToken ct)
    {
        try
        {
            using var conn = new SqlConnection(connStr);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            await conn.OpenAsync(timeoutCts.Token);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(timeoutCts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return false;
        }
    }

    private async Task<string?> ResolveConnectionStringAsync(int companyId, CancellationToken ct)
    {
        if (_resolvedTenantConnStrs.TryGetValue(companyId, out var cached))
        {
            if (await TestSingleConnectionAsync(cached, ct))
            {
                return cached;
            }
            _resolvedTenantConnStrs.TryRemove(companyId, out _);
        }

        foreach (var candidate in GetCandidateConnectionStrings(companyId))
        {
            if (await TestSingleConnectionAsync(candidate, ct))
            {
                _resolvedTenantConnStrs[companyId] = candidate;
                _isCloudOnline = true;
                _lastError = null;
                return candidate;
            }
        }

        return null;
    }

    public async Task<TenantCrmDbContext?> CreateCloudDbContextAsync(int companyId = 1, CancellationToken ct = default)
    {
        string? connStr = await ResolveConnectionStringAsync(companyId, ct);
        if (string.IsNullOrWhiteSpace(connStr))
        {
            _isCloudOnline = false;
            return null;
        }

        _isCloudOnline = true;

        var options = new DbContextOptionsBuilder<TenantCrmDbContext>()
            .UseSqlServer(connStr, sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new TenantCrmDbContext(options)
        {
            DisableSyncTracking = true,
            CurrentCompanyId = companyId
        };

        if (!_migratedTenants.ContainsKey(companyId))
        {
            try
            {
                await context.Database.MigrateAsync(ct);
                await EnsureCloudTenantBranchSchemaAsync(context, ct);
                _migratedTenants.TryAdd(companyId, true);
                _logger.LogInformation("Cloud database migrations and branch schema confirmed for company {CompanyId}.", companyId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-apply migrations for company {CompanyId}: {Message}", companyId, ex.Message);
            }
        }

        return context;
    }

    public async Task<CloudSyncStatus> GetStatusAsync(int companyId = 1, CancellationToken ct = default)
    {
        int pending = 0;
        int synced = 0;

        try
        {
            await using var localDb = await _localContextFactory.CreateAsync(companyId);
            pending = await localDb.SyncQueue.CountAsync(x => x.CompanyId == companyId && x.Status == "Pending", ct);
            synced = await localDb.SyncQueue.CountAsync(x => x.CompanyId == companyId && x.Status == "Synced", ct);
        }
        catch
        {
            // Local DB read fallback
        }

        string dbName = companyId switch
        {
            1 => "db70863 (Fixtech)",
            2 => "db70865 (Bytecare)",
            3 => "db70866 (Techrevive)",
            _ => "db70848 (MasterCRM)"
        };

        string msg = _isCloudOnline
            ? (pending == 0 ? $"Online: Local and MonsterASP Cloud ({dbName}) are synchronized." : $"Online: Storing to local and cloud. {pending} change(s) syncing to MonsterASP.")
            : (pending == 0 ? "Offline mode: Storing data locally. Will sync when MonsterASP is reachable." : $"Offline mode: {pending} local change(s) stored. Will automatically sync when reconnected.");

        return new CloudSyncStatus(
            IsCloudOnline: _isCloudOnline,
            PendingCount: pending,
            SyncedCount: synced,
            LastSyncTimeUtc: _lastSyncTimeUtc,
            CloudHost: "5.9.179.199 (MonsterASP: Port 1433)",
            CloudDatabase: dbName,
            LastError: _lastError,
            StatusMessage: msg
        );
    }

    public async Task<int> SyncPendingChangesAsync(int companyId = 1, CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(100, ct))
        {
            return 0; // Another sync cycle is currently executing
        }

        try
        {
            await using var localDb = await _localContextFactory.CreateAsync(companyId);
            localDb.DisableSyncTracking = true;

            var pendingItems = await localDb.SyncQueue
                .Where(x => x.CompanyId == companyId && x.Status == "Pending")
                .OrderBy(x => x.CreatedAtUtc)
                .Take(50)
                .ToListAsync(ct);

            if (pendingItems.Count == 0)
                return 0;

            await using var cloudDb = await CreateCloudDbContextAsync(companyId, ct);
            if (cloudDb == null) return 0;

            int processed = 0;

            foreach (var item in pendingItems)
            {
                try
                {
                    cloudDb.ChangeTracker.Clear();
                    await ApplyItemToCloudAsync(cloudDb, item, ct);
                    item.Status = "Synced";
                    item.SyncedAtUtc = DateTime.UtcNow;
                    item.LastError = null;
                    processed++;
                }
                catch (Exception ex)
                {
                    cloudDb.ChangeTracker.Clear();
                    _logger.LogWarning(ex, "Failed to sync item {Id} ({EntityType}:{EntityId}) to cloud: {Message}",
                        item.Id, item.EntityType, item.EntityId, ex.Message);
                    item.RetryCount++;
                    item.LastError = ex.Message;
                }
            }

            await localDb.SaveChangesAsync(ct);
            _lastSyncTimeUtc = DateTime.UtcNow;
            if (processed > 0)
            {
                _logger.LogInformation("Synchronized {Count} pending items for Company {CompanyId} to MonsterASP cloud database.", processed, companyId);
            }
            return processed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during cloud synchronization for company {CompanyId}.", companyId);
            _lastError = ex.Message;
            return 0;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task ApplyItemToCloudAsync(TenantCrmDbContext cloudDb, SyncQueueItem item, CancellationToken ct)
    {
        if (string.Equals(item.Operation, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            await HandleDeleteAsync(cloudDb, item, ct);
            return;
        }

        switch (item.EntityType)
        {
            case nameof(Customer):
                await SyncCustomerAsync(cloudDb, item, ct);
                break;
            case nameof(Device):
                await SyncDeviceAsync(cloudDb, item, ct);
                break;
            case nameof(RepairRequest):
                await SyncRepairRequestAsync(cloudDb, item, ct);
                break;
            case nameof(Payment):
                await SyncPaymentAsync(cloudDb, item, ct);
                break;
            case nameof(CustomerInteraction):
                await SyncInteractionAsync(cloudDb, item, ct);
                break;
            case nameof(FollowUp):
                await SyncFollowUpAsync(cloudDb, item, ct);
                break;
            case nameof(Supplier):
                await SyncSupplierAsync(cloudDb, item, ct);
                break;
            case nameof(Part):
                await SyncPartAsync(cloudDb, item, ct);
                break;
            case nameof(RepairPart):
                await SyncRepairPartAsync(cloudDb, item, ct);
                break;
            case nameof(RepairStatusHistory):
                await SyncRepairStatusHistoryAsync(cloudDb, item, ct);
                break;
            case nameof(RetentionRequest):
                await SyncRetentionRequestAsync(cloudDb, item, ct);
                break;
            case nameof(RetentionEmailTemplate):
                await SyncRetentionEmailTemplateAsync(cloudDb, item, ct);
                break;
            case nameof(RetentionEmailLog):
                await SyncRetentionEmailLogAsync(cloudDb, item, ct);
                break;
            case nameof(RetentionSettings):
                await SyncRetentionSettingsAsync(cloudDb, item, ct);
                break;
            default:
                _logger.LogDebug("Unhandled sync entity type {EntityType}", item.EntityType);
                break;
        }
    }

    private async Task HandleDeleteAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        if (!int.TryParse(item.EntityId, out int id)) return;

        switch (item.EntityType)
        {
            case nameof(Customer):
                var c = await db.Customers.FindAsync(new object[] { id }, ct);
                if (c != null) { db.Customers.Remove(c); await db.SaveChangesAsync(ct); }
                break;
            case nameof(Device):
                var d = await db.Devices.FindAsync(new object[] { id }, ct);
                if (d != null) { db.Devices.Remove(d); await db.SaveChangesAsync(ct); }
                break;
            case nameof(RepairRequest):
                var r = await db.RepairRequests.FindAsync(new object[] { id }, ct);
                if (r != null) { db.RepairRequests.Remove(r); await db.SaveChangesAsync(ct); }
                break;
            case nameof(Payment):
                var p = await db.Payments.FindAsync(new object[] { id }, ct);
                if (p != null) { db.Payments.Remove(p); await db.SaveChangesAsync(ct); }
                break;
            case nameof(CustomerInteraction):
                var ci = await db.CustomerInteractions.FindAsync(new object[] { id }, ct);
                if (ci != null) { db.CustomerInteractions.Remove(ci); await db.SaveChangesAsync(ct); }
                break;
            case nameof(FollowUp):
                var f = await db.FollowUps.FindAsync(new object[] { id }, ct);
                if (f != null) { db.FollowUps.Remove(f); await db.SaveChangesAsync(ct); }
                break;
            case nameof(Supplier):
                var s = await db.Suppliers.FindAsync(new object[] { id }, ct);
                if (s != null) { db.Suppliers.Remove(s); await db.SaveChangesAsync(ct); }
                break;
            case nameof(Part):
                var part = await db.Parts.FindAsync(new object[] { id }, ct);
                if (part != null) { db.Parts.Remove(part); await db.SaveChangesAsync(ct); }
                break;
            case nameof(RepairPart):
                var rp = await db.RepairParts.FindAsync(new object[] { id }, ct);
                if (rp != null) { db.RepairParts.Remove(rp); await db.SaveChangesAsync(ct); }
                break;
            case nameof(RepairStatusHistory):
                var rsh = await db.RepairStatusHistories.FindAsync(new object[] { id }, ct);
                if (rsh != null) { db.RepairStatusHistories.Remove(rsh); await db.SaveChangesAsync(ct); }
                break;
            case nameof(RetentionRequest):
                var rr = await db.RetentionRequests.FindAsync(new object[] { id }, ct);
                if (rr != null) { db.RetentionRequests.Remove(rr); await db.SaveChangesAsync(ct); }
                break;
            case nameof(RetentionEmailTemplate):
                var rt = await db.RetentionEmailTemplates.FindAsync(new object[] { id }, ct);
                if (rt != null) { db.RetentionEmailTemplates.Remove(rt); await db.SaveChangesAsync(ct); }
                break;
            case nameof(RetentionEmailLog):
                var rl = await db.RetentionEmailLogs.FindAsync(new object[] { id }, ct);
                if (rl != null) { db.RetentionEmailLogs.Remove(rl); await db.SaveChangesAsync(ct); }
                break;
        }
    }

    private async Task SyncCustomerAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<Customer>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.RepairRequests = new List<RepairRequest>();
        model.CustomerInteractions = new List<CustomerInteraction>();
        model.Devices = new List<Device>();
        model.Branch = null;

        var existing = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == model.CustomerId, ct);
        if (existing != null)
        {
            existing.FirstName = model.FirstName;
            existing.LastName = model.LastName;
            existing.Email = model.Email;
            existing.Phone = model.Phone;
            existing.Address = model.Address;
            existing.City = model.City;
            existing.StateOrProvince = model.StateOrProvince;
            existing.PostalCode = model.PostalCode;
            existing.Country = model.Country;
            existing.LoyaltyPoints = model.LoyaltyPoints;
            existing.IsActive = model.IsActive;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "Customers", async () =>
            {
                db.Customers.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncDeviceAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<Device>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.RepairRequests = new List<RepairRequest>();
        model.Customer = null;
        model.Company = null;
        model.Branch = null;

        var existing = await db.Devices.FirstOrDefaultAsync(x => x.DeviceId == model.DeviceId, ct);
        if (existing != null)
        {
            existing.DeviceName = model.DeviceName;
            existing.DeviceType = model.DeviceType;
            existing.Brand = model.Brand;
            existing.Model = model.Model;
            existing.SerialNumber = model.SerialNumber;
            existing.Status = model.Status;
            existing.WarrantyStatus = model.WarrantyStatus;
            existing.PurchasePrice = model.PurchasePrice;
            existing.CustomerId = model.CustomerId;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "Devices", async () =>
            {
                db.Devices.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRepairRequestAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RepairRequest>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Customer = null;
        model.Device = null;
        model.Branch = null;
        model.CustomerInteractions = new List<CustomerInteraction>();

        var existing = await db.RepairRequests.FirstOrDefaultAsync(x => x.RepairRequestId == model.RepairRequestId, ct);
        if (existing != null)
        {
            existing.RequestNumber = model.RequestNumber;
            existing.DeviceModel = model.DeviceModel;
            existing.SerialNumber = model.SerialNumber;
            existing.IssueDescription = model.IssueDescription;
            existing.TechnicianNotes = model.TechnicianNotes;
            existing.Status = model.Status;
            existing.Priority = model.Priority;
            existing.EstimatedCost = model.EstimatedCost;
            existing.ActualCost = model.ActualCost;
            existing.LaborCost = model.LaborCost;
            existing.PartsCost = model.PartsCost;
            existing.CompletionDate = model.CompletionDate;
            existing.AssignedToStaffId = model.AssignedToStaffId;
            existing.AssignedToManagerId = model.AssignedToManagerId;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "RepairRequests", async () =>
            {
                db.RepairRequests.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncPaymentAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<Payment>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.RepairRequest = null;
        model.Branch = null;

        var existing = await db.Payments.FirstOrDefaultAsync(x => x.PaymentId == model.PaymentId, ct);
        if (existing != null)
        {
            existing.Amount = model.Amount;
            existing.PaymentMethod = model.PaymentMethod;
            existing.ReferenceNumber = model.ReferenceNumber;
            existing.PaymentDate = model.PaymentDate;
            existing.IsPaid = model.IsPaid;
            existing.IsVoid = model.IsVoid;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "Payments", async () =>
            {
                db.Payments.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncInteractionAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<CustomerInteraction>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Customer = null;
        model.Branch = null;

        var existing = await db.CustomerInteractions.FirstOrDefaultAsync(x => x.CustomerInteractionId == model.CustomerInteractionId, ct);
        if (existing != null)
        {
            existing.InteractionType = model.InteractionType;
            existing.Status = model.Status;
            existing.Priority = model.Priority;
            existing.Subject = model.Subject;
            existing.Notes = model.Notes;
            existing.Resolution = model.Resolution;
            existing.InteractionByUserId = model.InteractionByUserId;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "CustomerInteractions", async () =>
            {
                db.CustomerInteractions.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncFollowUpAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<FollowUp>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Customer = null;
        model.RepairRequest = null;
        model.Branch = null;

        var existing = await db.FollowUps.FirstOrDefaultAsync(x => x.FollowUpId == model.FollowUpId, ct);
        if (existing != null)
        {
            existing.Subject = model.Subject;
            existing.Notes = model.Notes;
            existing.ScheduledAt = model.ScheduledAt;
            existing.CompletedAt = model.CompletedAt;
            existing.Channel = model.Channel;
            existing.Status = model.Status;
            existing.AssignedToUserId = model.AssignedToUserId;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "FollowUps", async () =>
            {
                db.FollowUps.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncSupplierAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<Supplier>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Parts = new List<Part>();

        var existing = await db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == model.SupplierId, ct);
        if (existing != null)
        {
            existing.SupplierCode = model.SupplierCode;
            existing.SupplierName = model.SupplierName;
            existing.ContactFirstName = model.ContactFirstName;
            existing.ContactLastName = model.ContactLastName;
            existing.ContactNumber = model.ContactNumber;
            existing.EmailAddress = model.EmailAddress;
            existing.Address = model.Address;
            existing.City = model.City;
            existing.StateOrProvince = model.StateOrProvince;
            existing.PostalCode = model.PostalCode;
            existing.Country = model.Country;
            existing.Notes = model.Notes;
            existing.IsActive = model.IsActive;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "Suppliers", async () =>
            {
                db.Suppliers.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncPartAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<Part>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Supplier = null;
        model.RepairParts = new List<RepairPart>();

        var existing = await db.Parts.FirstOrDefaultAsync(x => x.PartId == model.PartId, ct);
        if (existing != null)
        {
            existing.PartCode = model.PartCode;
            existing.PartName = model.PartName;
            existing.Category = model.Category;
            existing.Manufacturer = model.Manufacturer;
            existing.Model = model.Model;
            existing.UnitCost = model.UnitCost;
            existing.UnitPrice = model.UnitPrice;
            existing.QuantityOnHand = model.QuantityOnHand;
            existing.ReorderLevel = model.ReorderLevel;
            existing.SupplierId = model.SupplierId;
            existing.IsActive = model.IsActive;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "Parts", async () =>
            {
                db.Parts.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRepairPartAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RepairPart>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.RepairRequest = null;
        model.Part = null;

        var existing = await db.RepairParts.FirstOrDefaultAsync(x => x.RepairPartId == model.RepairPartId, ct);
        if (existing != null)
        {
            existing.QuantityUsed = model.QuantityUsed;
            existing.UnitCostAtTime = model.UnitCostAtTime;
            existing.UnitPriceAtTime = model.UnitPriceAtTime;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "RepairParts", async () =>
            {
                db.RepairParts.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRepairStatusHistoryAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RepairStatusHistory>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.RepairRequest = null;

        var existing = await db.RepairStatusHistories.FirstOrDefaultAsync(x => x.RepairStatusHistoryId == model.RepairStatusHistoryId, ct);
        if (existing == null)
        {
            await ExecuteWithIdentityInsertAsync(db, "RepairStatusHistories", async () =>
            {
                db.RepairStatusHistories.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRetentionRequestAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RetentionRequest>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Customer = null;
        model.EmailLogs = new List<RetentionEmailLog>();

        var existing = await db.RetentionRequests.FirstOrDefaultAsync(x => x.RetentionRequestId == model.RetentionRequestId, ct);
        if (existing != null)
        {
            existing.ProposedDiscountPercent = model.ProposedDiscountPercent;
            existing.Status = model.Status;
            existing.ReasonCategory = model.ReasonCategory;
            existing.ReasonNote = model.ReasonNote;
            existing.ActionType = model.ActionType;
            existing.RetentionDetails = model.RetentionDetails;
            existing.ReviewedByUserId = model.ReviewedByUserId;
            existing.ReviewedAt = model.ReviewedAt;
            existing.ReviewRemarks = model.ReviewRemarks;
            existing.RejectionReason = model.RejectionReason;
            existing.AddedToCampaign = model.AddedToCampaign;
            existing.CampaignAddedAt = model.CampaignAddedAt;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "RetentionRequests", async () =>
            {
                db.RetentionRequests.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRetentionEmailTemplateAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RetentionEmailTemplate>(item.PayloadJson, _jsonOpts);
        if (model == null) return;

        var existing = await db.RetentionEmailTemplates.FirstOrDefaultAsync(x => x.RetentionEmailTemplateId == model.RetentionEmailTemplateId, ct);
        if (existing != null)
        {
            existing.TemplateName = model.TemplateName;
            existing.Subject = model.Subject;
            existing.Body = model.Body;
            existing.DefaultDiscountPercent = model.DefaultDiscountPercent;
            existing.ValidityDays = model.ValidityDays;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "RetentionEmailTemplates", async () =>
            {
                db.RetentionEmailTemplates.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRetentionEmailLogAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RetentionEmailLog>(item.PayloadJson, _jsonOpts);
        if (model == null) return;
        model.Customer = null;
        model.RetentionRequest = null;

        var existing = await db.RetentionEmailLogs.FirstOrDefaultAsync(x => x.RetentionEmailLogId == model.RetentionEmailLogId, ct);
        if (existing == null)
        {
            await ExecuteWithIdentityInsertAsync(db, "RetentionEmailLogs", async () =>
            {
                db.RetentionEmailLogs.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task SyncRetentionSettingsAsync(TenantCrmDbContext db, SyncQueueItem item, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<RetentionSettings>(item.PayloadJson, _jsonOpts);
        if (model == null) return;

        var existing = await db.RetentionSettings.FirstOrDefaultAsync(x => x.RetentionSettingsId == model.RetentionSettingsId, ct);
        if (existing != null)
        {
            existing.InactiveThresholdDays = model.InactiveThresholdDays;
            existing.AtRiskThresholdDays = model.AtRiskThresholdDays;
            existing.AntiFatigueDays = model.AntiFatigueDays;
            existing.DefaultOfferValidityDays = model.DefaultOfferValidityDays;
            existing.SmtpHost = model.SmtpHost;
            existing.SmtpPort = model.SmtpPort;
            existing.SmtpUsername = model.SmtpUsername;
            existing.SmtpPassword = model.SmtpPassword;
            existing.SmtpFromEmail = model.SmtpFromEmail;
            existing.SmtpFromName = model.SmtpFromName;
            existing.SmtpEnableSsl = model.SmtpEnableSsl;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await ExecuteWithIdentityInsertAsync(db, "RetentionSettings", async () =>
            {
                db.RetentionSettings.Add(model);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private async Task ExecuteWithIdentityInsertAsync(TenantCrmDbContext db, string table, Func<Task> action, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        string sqlOn = $"SET IDENTITY_INSERT [{table}] ON;";
        string sqlOff = $"SET IDENTITY_INSERT [{table}] OFF;";

        if (db.Database.CurrentTransaction != null)
        {
            await db.Database.ExecuteSqlRawAsync(sqlOn, ct);
            try
            {
                await action();
            }
            finally
            {
                try { await db.Database.ExecuteSqlRawAsync(sqlOff, ct); } catch { }
            }
        }
        else
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    await db.Database.ExecuteSqlRawAsync(sqlOn, ct);
                    await action();
                    await db.Database.ExecuteSqlRawAsync(sqlOff, ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    try { await db.Database.ExecuteSqlRawAsync(sqlOff, ct); } catch { }
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });
        }
    }

    private static async Task EnsureCloudTenantBranchSchemaAsync(TenantCrmDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Branches')
BEGIN
    CREATE TABLE Branches (
        BranchId INT IDENTITY(1,1) PRIMARY KEY,
        CompanyId INT NULL,
        BranchCode NVARCHAR(50) NOT NULL,
        BranchName NVARCHAR(200) NOT NULL,
        Address NVARCHAR(500) NULL,
        City NVARCHAR(100) NULL,
        StateOrProvince NVARCHAR(100) NULL,
        PostalCode NVARCHAR(20) NULL,
        Phone NVARCHAR(50) NULL,
        Email NVARCHAR(200) NULL,
        ManagerUserId NVARCHAR(450) NULL,
        ManagerName NVARCHAR(200) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Branches_BranchCode' AND object_id = OBJECT_ID('Branches'))
BEGIN
    CREATE UNIQUE INDEX IX_Branches_BranchCode ON Branches(BranchCode);
END

IF COL_LENGTH('Customers', 'BranchId') IS NULL ALTER TABLE Customers ADD BranchId INT NULL;
IF COL_LENGTH('RepairRequests', 'BranchId') IS NULL ALTER TABLE RepairRequests ADD BranchId INT NULL;
IF COL_LENGTH('Devices', 'BranchId') IS NULL ALTER TABLE Devices ADD BranchId INT NULL;
IF COL_LENGTH('CustomerInteractions', 'BranchId') IS NULL ALTER TABLE CustomerInteractions ADD BranchId INT NULL;
IF COL_LENGTH('FollowUps', 'BranchId') IS NULL ALTER TABLE FollowUps ADD BranchId INT NULL;
IF COL_LENGTH('Payments', 'BranchId') IS NULL ALTER TABLE Payments ADD BranchId INT NULL;
", ct);
    }
}
