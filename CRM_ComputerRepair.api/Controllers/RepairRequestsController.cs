using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/repair-requests")]
public class RepairRequestsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly IAuditWriter _audit;

    public RepairRequestsController(ITenantDbContextFactory factory, IAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    // ─── GET ALL ───
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId, [FromQuery] RepairStatus? status)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.RepairRequests.AsNoTracking();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var list = await query.OrderByDescending(x => x.RequestDate).ToListAsync();
        return Ok(list);
    }

    // ─── GET ONE ───
    [HttpGet("{repairRequestId:int}")]
    public async Task<IActionResult> GetById(int companyId, int repairRequestId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await db.RepairRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);
        return item is null ? NotFound() : Ok(item);
    }

    // ─── CREATE ───
    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreateRepairRequestRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var rr = new RepairRequest
        {
            RequestNumber = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6]}",
            CustomerId = request.CustomerId,
            DeviceId = request.DeviceId,
            DeviceModel = request.DeviceModel.Trim(),
            SerialNumber = request.SerialNumber?.Trim() ?? "",
            IssueDescription = request.IssueDescription.Trim(),
            Priority = (Priority)request.Priority,
            Status = RepairStatus.Pending,
            EstimatedCost = request.EstimatedCost,
            RequestDate = DateTime.UtcNow
        };

        db.RepairRequests.Add(rr);
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Create", "RepairRequest",
            rr.RepairRequestId.ToString(), rr.RequestNumber);

        return CreatedAtAction(nameof(GetById),
            new { companyId, repairRequestId = rr.RepairRequestId }, rr);
    }

    // ─── UPDATE ───
    [HttpPut("{repairRequestId:int}")]
    public async Task<IActionResult> Update(
        int companyId, int repairRequestId,
        [FromBody] UpdateRepairRequestRequest request,
        [FromQuery] string? changedByUserId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.RepairRequests
            .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

        if (item is null) return NotFound();

        var oldStatus = item.Status;

        item.CustomerId = request.CustomerId;
        item.DeviceId = request.DeviceId;
        item.DeviceModel = request.DeviceModel.Trim();
        item.SerialNumber = request.SerialNumber?.Trim() ?? "";
        item.IssueDescription = request.IssueDescription.Trim();
        item.Priority = (Priority)request.Priority;
        item.Status = (RepairStatus)request.Status;
        item.EstimatedCost = request.EstimatedCost;
        item.ActualCost = request.ActualCost;
        item.PartsCost = request.PartsCost;
        item.LaborCost = request.LaborCost;
        item.TechnicianNotes = request.TechnicianNotes?.Trim();
        item.AssignedToStaffId = request.AssignedToStaffId;
        item.AssignedToManagerId = request.AssignedToManagerId;

        if (item.Status == RepairStatus.Completed && item.CompletionDate is null)
            item.CompletionDate = DateTime.UtcNow;

        if (oldStatus != item.Status)
        {
            db.RepairStatusHistories.Add(new RepairStatusHistory
            {
                RepairRequestId = item.RepairRequestId,
                OldStatus = oldStatus,
                NewStatus = item.Status,
                ChangedByUserId = changedByUserId ?? UserSessionHelper.GetUserId(HttpContext),
                ChangedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        // ─── Audit row ───
        var actor = changedByUserId ?? UserSessionHelper.GetUserId(HttpContext);
        var details = oldStatus != item.Status
            ? $"{item.RequestNumber}: {oldStatus} → {item.Status}"
            : $"{item.RequestNumber}: updated";

        await _audit.WriteAsync(
            actor,
            "Update", "RepairRequest",
            item.RepairRequestId.ToString(),
            details);

        return Ok(item);
    }
}