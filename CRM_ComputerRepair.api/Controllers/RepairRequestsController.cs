using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using CRM_ComputerRepair.api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/repair-requests")]
[Authorize(Roles = "Staff,Manager,Admin,Super Admin")]
[RequireSubscribedModule(ModuleCodes.MainTransactions, ModuleCodes.BusinessIntelligence)]
public class RepairRequestsController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly IAuditWriter _audit;
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _userManager;

    public RepairRequestsController(
        ITenantDbContextFactory factory,
        IAuditWriter audit,
        Microsoft.AspNetCore.Identity.UserManager<User> userManager)
    {
        _factory = factory;
        _audit = audit;
        _userManager = userManager;
    }

    private static async Task<RepairRequestResponseDto?> LoadResponseDtoAsync(
        TenantCrmDbContext db,
        int repairRequestId)
    {
        return await db.RepairRequests.AsNoTracking()
            .Where(r => r.RepairRequestId == repairRequestId)
            .Include(r => r.Customer)
            .Include(r => r.Branch)
            .Select(r => new RepairRequestResponseDto
            {
                RepairRequestId = r.RepairRequestId,
                RequestNumber = r.RequestNumber,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer != null ? (r.Customer.FirstName + " " + r.Customer.LastName).Trim() : null,
                CustomerPhone = r.Customer != null ? r.Customer.Phone : null,
                CustomerEmail = r.Customer != null ? r.Customer.Email : null,
                DeviceId = r.DeviceId,
                DeviceModel = r.DeviceModel,
                SerialNumber = r.SerialNumber,
                IssueDescription = r.IssueDescription,
                Status = (int)r.Status,
                Priority = (int)r.Priority,
                RequestDate = r.RequestDate,
                CompletionDate = r.CompletionDate,
                EstimatedCost = r.EstimatedCost,
                ActualCost = r.ActualCost,
                PartsCost = r.PartsCost,
                LaborCost = r.LaborCost,
                TechnicianNotes = r.TechnicianNotes,
                AssignedToStaffId = r.AssignedToStaffId,
                AssignedToManagerId = r.AssignedToManagerId,
                BranchId = r.BranchId,
                BranchName = r.Branch != null ? r.Branch.BranchName : null
            })
            .FirstOrDefaultAsync();
    }

    // --- GET ALL ---
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int companyId,
        [FromQuery] RepairStatus? status,
        [FromQuery] string? search = null,
        [FromQuery] int? branchId = null)
    {
        var (scopedBranchId, isAllowed) = await BranchScopeHelper.ResolveBranchScopeAsync(HttpContext, _userManager, branchId);
        if (!isAllowed)
            return Forbid();

        await using var db = await _factory.CreateAsync(companyId);

        var query = db.RepairRequests.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Branch)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (scopedBranchId.HasValue)
            query = query.Where(x => x.BranchId == scopedBranchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.RequestNumber.Contains(s) ||
                x.DeviceModel.Contains(s) ||
                x.SerialNumber.Contains(s) ||
                x.IssueDescription.Contains(s) ||
                (x.Customer != null && (x.Customer.FirstName.Contains(s) || x.Customer.LastName.Contains(s) || (x.Customer.Phone != null && x.Customer.Phone.Contains(s)))));
        }

        var list = await query.OrderByDescending(x => x.RequestDate)
            .Select(r => new RepairRequestResponseDto
            {
                RepairRequestId = r.RepairRequestId,
                RequestNumber = r.RequestNumber,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer != null ? (r.Customer.FirstName + " " + r.Customer.LastName).Trim() : null,
                CustomerPhone = r.Customer != null ? r.Customer.Phone : null,
                CustomerEmail = r.Customer != null ? r.Customer.Email : null,
                DeviceId = r.DeviceId,
                DeviceModel = r.DeviceModel,
                SerialNumber = r.SerialNumber,
                IssueDescription = r.IssueDescription,
                Status = (int)r.Status,
                Priority = (int)r.Priority,
                RequestDate = r.RequestDate,
                CompletionDate = r.CompletionDate,
                EstimatedCost = r.EstimatedCost,
                ActualCost = r.ActualCost,
                PartsCost = r.PartsCost,
                LaborCost = r.LaborCost,
                TechnicianNotes = r.TechnicianNotes,
                AssignedToStaffId = r.AssignedToStaffId,
                AssignedToManagerId = r.AssignedToManagerId,
                BranchId = r.BranchId,
                BranchName = r.Branch != null ? r.Branch.BranchName : null
            })
            .ToListAsync();

        return Ok(list);
    }

    // --- GET ONE ---
    [HttpGet("{repairRequestId:int}")]
    public async Task<IActionResult> GetById(int companyId, int repairRequestId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var item = await LoadResponseDtoAsync(db, repairRequestId);
        return item is null ? NotFound() : Ok(item);
    }

    // --- CREATE ---
    [HttpPost]
    public async Task<IActionResult> Create(
        int companyId, [FromBody] CreateRepairRequestRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (scopedBranchId, _) = await BranchScopeHelper.ResolveBranchScopeAsync(HttpContext, _userManager, request.BranchId);

        await using var db = await _factory.CreateAsync(companyId);

        var rr = new RepairRequest
        {
            RequestNumber = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            CustomerId = request.CustomerId,
            DeviceId = request.DeviceId,
            DeviceModel = request.DeviceModel.Trim(),
            SerialNumber = request.SerialNumber?.Trim() ?? "",
            IssueDescription = request.IssueDescription.Trim(),
            Priority = (Priority)request.Priority,
            Status = RepairStatus.Pending,
            EstimatedCost = request.EstimatedCost,
            BranchId = request.BranchId ?? scopedBranchId,
            RequestDate = DateTime.UtcNow
        };

        db.RepairRequests.Add(rr);
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            UserSessionHelper.GetUserId(HttpContext),
            "Create", "RepairRequest",
            rr.RepairRequestId.ToString(), rr.RequestNumber);

        var responseDto = await LoadResponseDtoAsync(db, rr.RepairRequestId);
        return CreatedAtAction(nameof(GetById),
            new { companyId, repairRequestId = rr.RepairRequestId }, responseDto);
    }

    // --- UPDATE ---
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
        if (request.BranchId.HasValue)
            item.BranchId = request.BranchId.Value;

        if (item.Status == RepairStatus.Completed && item.CompletionDate is null)
            item.CompletionDate = DateTime.UtcNow;
        if (item.Status != RepairStatus.Completed)
            item.CompletionDate = null;

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

        // --- Audit row ---
        var actor = changedByUserId ?? UserSessionHelper.GetUserId(HttpContext);
        var details = oldStatus != item.Status
            ? $"{item.RequestNumber}: {oldStatus} -> {item.Status}"
            : $"{item.RequestNumber}: updated";

        await _audit.WriteAsync(
            actor,
            "Update", "RepairRequest",
            item.RepairRequestId.ToString(),
            details);

        var responseDto = await LoadResponseDtoAsync(db, item.RepairRequestId);
        return Ok(responseDto);
    }

    // --- QUICK STATUS CHANGE ---
    [HttpPost("{repairRequestId:int}/status")]
    public async Task<IActionResult> ChangeStatus(
        int companyId, int repairRequestId,
        [FromBody] ChangeRepairStatusRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.RepairRequests
            .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

        if (item is null) return NotFound();

        var oldStatus = item.Status;
        item.Status = (RepairStatus)request.Status;

        if (item.Status == RepairStatus.Completed && item.CompletionDate is null)
            item.CompletionDate = DateTime.UtcNow;
        if (item.Status != RepairStatus.Completed)
            item.CompletionDate = null;

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            item.TechnicianNotes = string.IsNullOrWhiteSpace(item.TechnicianNotes)
                ? request.Notes.Trim()
                : $"{item.TechnicianNotes}\n[{DateTime.UtcNow:MMM d, HH:mm}] {request.Notes.Trim()}";
        }

        if (oldStatus != item.Status)
        {
            db.RepairStatusHistories.Add(new RepairStatusHistory
            {
                RepairRequestId = item.RepairRequestId,
                OldStatus = oldStatus,
                NewStatus = item.Status,
                ChangedByUserId = UserSessionHelper.GetUserId(HttpContext),
                ChangedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var actor = UserSessionHelper.GetUserId(HttpContext);
        await _audit.WriteAsync(
            actor,
            "StatusChange", "RepairRequest",
            item.RepairRequestId.ToString(),
            $"{item.RequestNumber}: {oldStatus} -> {item.Status}");

        var responseDto = await LoadResponseDtoAsync(db, item.RepairRequestId);
        return Ok(responseDto);
    }

    // --- QUICK COMPLETE ---
    [HttpPost("{repairRequestId:int}/quick-complete")]
    public async Task<IActionResult> QuickComplete(
        int companyId, int repairRequestId,
        [FromBody] QuickCompleteRepairRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.RepairRequests
            .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

        if (item is null) return NotFound();

        var oldStatus = item.Status;
        item.Status = RepairStatus.Completed;
        item.CompletionDate = DateTime.UtcNow;

        if (request.ActualCost.HasValue) item.ActualCost = request.ActualCost;
        if (request.PartsCost.HasValue) item.PartsCost = request.PartsCost;
        if (request.LaborCost.HasValue) item.LaborCost = request.LaborCost;

        if (!string.IsNullOrWhiteSpace(request.TechnicianNotes))
        {
            item.TechnicianNotes = string.IsNullOrWhiteSpace(item.TechnicianNotes)
                ? request.TechnicianNotes.Trim()
                : $"{item.TechnicianNotes}\n[Completed {DateTime.UtcNow:MMM d, HH:mm}] {request.TechnicianNotes.Trim()}";
        }

        if (oldStatus != item.Status)
        {
            db.RepairStatusHistories.Add(new RepairStatusHistory
            {
                RepairRequestId = item.RepairRequestId,
                OldStatus = oldStatus,
                NewStatus = item.Status,
                ChangedByUserId = UserSessionHelper.GetUserId(HttpContext),
                ChangedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var actor = UserSessionHelper.GetUserId(HttpContext);
        await _audit.WriteAsync(
            actor,
            "QuickComplete", "RepairRequest",
            item.RepairRequestId.ToString(),
            $"{item.RequestNumber}: Completed with actual cost {item.ActualCost:C}");

        var responseDto = await LoadResponseDtoAsync(db, item.RepairRequestId);
        return Ok(responseDto);
    }

    // --- APPROVE (Manager+) ---
    [HttpPost("{repairRequestId:int}/approve")]
    [Authorize(Roles = "Manager,Admin,Super Admin")]
    public async Task<IActionResult> Approve(
        int companyId, int repairRequestId,
        [FromBody] ApproveRepairRequestRequest request,
        [FromQuery] string? managerUserId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.RepairRequests
            .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

        if (item is null) return NotFound();

        if (item.Status != RepairStatus.Pending)
            return BadRequest(new { message =
                $"Cannot approve: request is {item.Status}. Only pending requests can be approved." });

        var oldStatus = item.Status;

        item.Status = RepairStatus.Approved;
        item.AssignedToManagerId = managerUserId ?? UserSessionHelper.GetUserId(HttpContext);
        if (!string.IsNullOrWhiteSpace(request.AssignedToStaffId))
            item.AssignedToStaffId = request.AssignedToStaffId.Trim();
        if (request.EstimatedCost.HasValue)
            item.EstimatedCost = request.EstimatedCost;
        if (!string.IsNullOrWhiteSpace(request.ManagerNotes))
            item.TechnicianNotes = request.ManagerNotes.Trim();

        db.RepairStatusHistories.Add(new RepairStatusHistory
        {
            RepairRequestId = item.RepairRequestId,
            OldStatus = oldStatus,
            NewStatus = item.Status,
            ChangedByUserId = managerUserId ?? UserSessionHelper.GetUserId(HttpContext),
            ChangedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            managerUserId ?? UserSessionHelper.GetUserId(HttpContext),
            "Approve", "RepairRequest",
            item.RepairRequestId.ToString(),
            $"{item.RequestNumber}: {oldStatus} -> {item.Status}");

        var responseDto = await LoadResponseDtoAsync(db, item.RepairRequestId);
        return Ok(responseDto);
    }

    // --- REASSIGN (Manager+) ---
    [HttpPost("{repairRequestId:int}/reassign")]
    [Authorize(Roles = "Manager,Admin,Super Admin")]
    public async Task<IActionResult> Reassign(
        int companyId, int repairRequestId,
        [FromBody] ReassignRepairRequestRequest request,
        [FromQuery] string? managerUserId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var item = await db.RepairRequests
            .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

        if (item is null) return NotFound();

        var oldStatus = item.Status;

        item.AssignedToStaffId = request.AssignedToStaffId.Trim();
        item.Status = RepairStatus.Reassigned;

        if (!string.IsNullOrWhiteSpace(request.Notes))
            item.TechnicianNotes = request.Notes.Trim();

        db.RepairStatusHistories.Add(new RepairStatusHistory
        {
            RepairRequestId = item.RepairRequestId,
            OldStatus = oldStatus,
            NewStatus = item.Status,
            ChangedByUserId = managerUserId ?? UserSessionHelper.GetUserId(HttpContext),
            ChangedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            managerUserId ?? UserSessionHelper.GetUserId(HttpContext),
            "Reassign", "RepairRequest",
            item.RepairRequestId.ToString(),
            $"{item.RequestNumber}: reassigned to {request.AssignedToStaffId}");

        var responseDto = await LoadResponseDtoAsync(db, item.RepairRequestId);
        return Ok(responseDto);
    }
}