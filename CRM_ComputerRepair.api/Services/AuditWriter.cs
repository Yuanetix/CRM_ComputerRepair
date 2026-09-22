using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;

namespace CRM_ComputerRepair.api.Services;

public interface IAuditWriter
{
    Task WriteAsync(string? userId, string action, string entity,
        string? entityId = null, string? details = null);
}

public class AuditWriter : IAuditWriter
{
    private readonly MasterCrmDbContext _db;

    public AuditWriter(MasterCrmDbContext db) => _db = db;

    public async Task WriteAsync(string? userId, string action, string entity,
        string? entityId = null, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}