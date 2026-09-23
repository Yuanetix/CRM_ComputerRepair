using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("audit")]
[Authorize(Roles = "Manager,Admin,Super Admin")]
public class AuditController : ControllerBase
{
    private readonly MasterCrmDbContext _db;

    public AuditController(MasterCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? userId = null,
        [FromQuery] string? entity = null,
        [FromQuery] int take = 200)
    {
        if (take < 1) take = 1;
        if (take > 2000) take = 2000;

        var query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(x => x.Entity == entity);

        var list = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(x => new AuditLogDto
            {
                AuditLogId = x.AuditLogId,
                UserId = x.UserId,
                Action = x.Action,
                Entity = x.Entity,
                EntityId = x.EntityId,
                Details = x.Details,
                Timestamp = x.Timestamp
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int take = 50)
    {
        if (take < 1) take = 1;
        if (take > 500) take = 500;

        var list = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(x => new AuditLogDto
            {
                AuditLogId = x.AuditLogId,
                UserId = x.UserId,
                Action = x.Action,
                Entity = x.Entity,
                EntityId = x.EntityId,
                Details = x.Details,
                Timestamp = x.Timestamp
            })
            .ToListAsync();

        return Ok(list);
    }
}