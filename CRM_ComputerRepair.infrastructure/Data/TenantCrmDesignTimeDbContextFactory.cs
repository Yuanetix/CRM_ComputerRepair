using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CRM_ComputerRepair.infrastructure.Data;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build the tenant context
/// from the API project's connection string.
/// </summary>
public class TenantCrmDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<TenantCrmDbContext>
{
    public TenantCrmDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "CRM_ComputerRepair.api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("TenantCrm");

        var optionsBuilder = new DbContextOptionsBuilder<TenantCrmDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new TenantCrmDbContext(optionsBuilder.Options);
    }
}