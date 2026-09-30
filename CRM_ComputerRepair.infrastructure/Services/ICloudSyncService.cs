using CRM_ComputerRepair.infrastructure.Data;

namespace CRM_ComputerRepair.infrastructure.Services;

public record CloudSyncStatus(
    bool IsCloudOnline,
    int PendingCount,
    int SyncedCount,
    DateTime? LastSyncTimeUtc,
    string CloudHost,
    string CloudDatabase,
    string? LastError,
    string StatusMessage
);

public interface ICloudSyncService
{
    bool IsCloudOnline { get; }
    Task<bool> CheckConnectivityAsync(CancellationToken ct = default);
    Task<CloudSyncStatus> GetStatusAsync(int companyId = 1, CancellationToken ct = default);
    Task<int> SyncPendingChangesAsync(int companyId = 1, CancellationToken ct = default);
    Task<TenantCrmDbContext?> CreateCloudDbContextAsync(int companyId = 1, CancellationToken ct = default);
}
