using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CRM_ComputerRepair.infrastructure.Services;

public class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly ITenantDatabaseResolver _resolver;
    private readonly IConfiguration _configuration;

    public TenantDbContextFactory(
        ITenantDatabaseResolver resolver,
        IConfiguration configuration)
    {
        _resolver = resolver;
        _configuration = configuration;
    }

    public async Task<TenantCrmDbContext> CreateAsync(int companyId)
    {
        var databaseInfo = await _resolver.GetDatabaseInfoAsync(companyId);

        var userId = _configuration[
            $"TenantCredentials:{databaseInfo.CredentialKey}:UserId"];

        var password = _configuration[
            $"TenantCredentials:{databaseInfo.CredentialKey}:Password"];

        string connectionString;

        // If CredentialKey is empty, use Windows Auth (LocalDB / dev).
        // Otherwise, use SQL login credentials.
        if (string.IsNullOrWhiteSpace(databaseInfo.CredentialKey))
        {
            connectionString =
                $"Server={databaseInfo.ServerName};" +
                $"Database={databaseInfo.DatabaseName};" +
                $"Trusted_Connection=True;" +
                $"TrustServerCertificate=True;" +
                $"MultipleActiveResultSets=True;";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Credentials not found for key '{databaseInfo.CredentialKey}'.");
            }

            connectionString =
                $"Server={databaseInfo.ServerName};" +
                $"Database={databaseInfo.DatabaseName};" +
                $"User Id={userId};" +
                $"Password={password};" +
                $"Encrypt=True;" +
                $"TrustServerCertificate=True;" +
                $"MultipleActiveResultSets=True;";
        }

        var options = new DbContextOptionsBuilder<TenantCrmDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new TenantCrmDbContext(options);
    }
}