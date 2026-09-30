using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;

namespace CRM_ComputerRepair.api.Services;

public class CloudSyncBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CloudSyncBackgroundWorker> _logger;
    private static readonly AutoResetEvent _wakeUp = new(false);

    public CloudSyncBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<CloudSyncBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Auto-trigger sync as soon as any local database transaction commits
        TenantCrmDbContext.OnLocalChangesSaved += TriggerImmediateSync;
    }

    public static void TriggerImmediateSync()
    {
        _wakeUp.Set();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cloud Sync background worker started.");

        // Initial delay to let the app start cleanly
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        // Active tenant companies (1 = Fixtech, 2 = Bytecare, 3 = Techrevive)
        var companyIds = new[] { 1, 2, 3 };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ICloudSyncService>();

                // Check connectivity to MonsterASP cloud databases
                bool online = await syncService.CheckConnectivityAsync(stoppingToken);

                if (online)
                {
                    // Automatically flush any pending updates recorded locally while offline
                    foreach (var companyId in companyIds)
                    {
                        try
                        {
                            int synced = await syncService.SyncPendingChangesAsync(companyId, stoppingToken);
                            if (synced > 0)
                            {
                                _logger.LogInformation("Background worker synced {Count} records to MonsterASP for company {CompanyId}.", synced, companyId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed background sync for company {CompanyId}: {Message}", companyId, ex.Message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background cloud sync cycle failed: {Message}", ex.Message);
            }

            // Wait 10 seconds or until woken up by an immediate data change
            try
            {
                await Task.Run(() => _wakeUp.WaitOne(TimeSpan.FromSeconds(10)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Cloud Sync background worker stopped.");
    }
}
