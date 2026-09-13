using CRM_ComputerRepair.infrastructure.Data;

namespace CRM_ComputerRepair.infrastructure.Services;

public interface ITenantDbContextFactory
{
    Task<TenantCrmDbContext> CreateAsync(int companyId);
}