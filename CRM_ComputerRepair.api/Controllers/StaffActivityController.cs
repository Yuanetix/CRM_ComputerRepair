using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/staff-activity")]
public class StaffActivityController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;

    public StaffActivityController(ITenantDbContextFactory factory) => _factory = factory;

    /// <summary>
    /// Recent repair status changes, joined with request info.
    /// Optionally filter by staff ID.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] string? staffId = null,
        [FromQuery] int take = 200)
    {
        await using var db = await _factory.CreateAsync(companyId);

        if (take < 1) take = 1;
        if (take > 1000) take = 1000;

        var query =
            from h in db.RepairStatusHistories.AsNoTracking()
            join r in db.RepairRequests.AsNoTracking()
                on h.RepairRequestId equals r.RepairRequestId
            orderby h.ChangedAt descending
            select new StaffActivityDto
            {
                RepairStatusHistoryId = h.RepairStatusHistoryId,
                RepairRequestId = h.RepairRequestId,
                RequestNumber = r.RequestNumber,
                DeviceModel = r.DeviceModel,
                OldStatus = (int)h.OldStatus,
                NewStatus = (int)h.NewStatus,
                ChangedByUserId = h.ChangedByUserId,
                Notes = h.Notes,
                ChangedAt = h.ChangedAt
            };

        if (!string.IsNullOrWhiteSpace(staffId))
            query = query.Where(x => x.ChangedByUserId == staffId);

        var list = await query.Take(take).ToListAsync();
        return Ok(list);
    }
}