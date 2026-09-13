using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.infrastructure.Services;

public class TenantDatabaseResolver : ITenantDatabaseResolver
{
    private readonly MasterCrmDbContext _masterDb;

    public TenantDatabaseResolver(MasterCrmDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    public async Task<TenantDatabaseInfo> GetDatabaseInfoAsync(int companyId)
    {
        var tenantDatabase = await _masterDb.CompanyDatabases
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.CompanyId == companyId &&
                x.IsActive);

        if (tenantDatabase == null)
        {
            throw new InvalidOperationException(
                $"No active tenant database found for CompanyId {companyId}.");
        }

        return new TenantDatabaseInfo
        {
            ServerName = tenantDatabase.ServerName,
            DatabaseName = tenantDatabase.DatabaseName,
            CredentialKey = tenantDatabase.CredentialKey
        };
    }
}