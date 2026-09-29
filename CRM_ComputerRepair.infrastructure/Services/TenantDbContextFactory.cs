using System.Collections.Concurrent;
using CRM_ComputerRepair.infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRM_ComputerRepair.infrastructure.Services;

public class TenantDbContextFactory : ITenantDbContextFactory
{
    private static readonly ConcurrentDictionary<int, bool> _initializedTenants = new();
    private readonly ITenantDatabaseResolver _resolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDbContextFactory> _logger;

    public TenantDbContextFactory(
        ITenantDatabaseResolver resolver,
        IConfiguration configuration,
        ILogger<TenantDbContextFactory> logger)
    {
        _resolver = resolver;
        _configuration = configuration;
        _logger = logger;
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
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new TenantCrmDbContext(options);

        // Ensure database exists and schema is migrated on first access in this app domain
        if (!_initializedTenants.ContainsKey(companyId))
        {
            try
            {
                await context.Database.MigrateAsync();
                _initializedTenants.TryAdd(companyId, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-migrate tenant database for CompanyId {CompanyId} ({DatabaseName}).", companyId, databaseInfo.DatabaseName);
            }
        }

        return context;
    }
}