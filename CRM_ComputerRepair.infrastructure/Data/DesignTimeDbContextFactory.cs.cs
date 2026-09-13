using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace CRM_ComputerRepair.infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MasterCrmDbContext>
{
    public MasterCrmDbContext CreateDbContext(string[] args)
    {
        // Build path to the API project's appsettings.json
        var apiProjectPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "CRM_ComputerRepair.api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("MasterCrm");

        var optionsBuilder = new DbContextOptionsBuilder<MasterCrmDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new MasterCrmDbContext(optionsBuilder.Options);
    }
}