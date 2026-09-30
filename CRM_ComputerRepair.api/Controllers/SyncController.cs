using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("api/sync")]
[AllowAnonymous] // Allow client UI to check sync state freely
public class SyncController : ControllerBase
{
    private readonly ICloudSyncService _syncService;

    public SyncController(ICloudSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] int companyId = 1)
    {
        var status = await _syncService.GetStatusAsync(companyId);
        return Ok(status);
    }

    [HttpPost("now")]
    public async Task<IActionResult> TriggerSyncNow([FromQuery] int companyId = 1)
    {
        // 1. Wake background worker for any following cycles
        CloudSyncBackgroundWorker.TriggerImmediateSync();

        // 2. Perform direct sync attempt immediately
        int synced = await _syncService.SyncPendingChangesAsync(companyId);
        var status = await _syncService.GetStatusAsync(companyId);

        return Ok(new
        {
            syncedCount = synced,
            status
        });
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestConnection()
    {
        bool online = await _syncService.CheckConnectivityAsync();
        var status = await _syncService.GetStatusAsync();

        return Ok(new
        {
            isOnline = online,
            details = status
        });
    }
}
