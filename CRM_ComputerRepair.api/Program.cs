using Microsoft.EntityFrameworkCore;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using CRM_ComputerRepair.domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// ── Master DB ──
builder.Services.AddDbContext<MasterCrmDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MasterCrm")));

// ── Tenant DB (for migrations / design-time only) ──
builder.Services.AddDbContext<TenantCrmDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TenantCrm")));

// ── Multi-tenant services ──
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ═══════════════════════════════════════════════════════════
// MASTER DB ENDPOINTS
// ═══════════════════════════════════════════════════════════

app.MapPost("/companies", async (Company company, MasterCrmDbContext db) =>
{
    db.Companies.Add(company);
    await db.SaveChangesAsync();
    return Results.Created($"/companies/{company.CompanyId}", company);
});

app.MapGet("/companies", async (MasterCrmDbContext db) =>
{
    var companies = await db.Companies
        .Include(c => c.Devices)
        .ToListAsync();
    return Results.Ok(companies);
});

app.MapGet("/companies/{id:int}", async (int id, MasterCrmDbContext db) =>
{
    var company = await db.Companies
        .Include(c => c.Devices)
        .FirstOrDefaultAsync(c => c.CompanyId == id);
    return company is null ? Results.NotFound() : Results.Ok(company);
});

app.MapPost("/devices", async (Device device, MasterCrmDbContext db) =>
{
    db.Devices.Add(device);
    await db.SaveChangesAsync();
    return Results.Created($"/devices/{device.DeviceId}", device);
});

app.MapGet("/devices", async (MasterCrmDbContext db) =>
{
    var devices = await db.Devices
        .Include(d => d.Company)
        .ToListAsync();
    return Results.Ok(devices);
});

app.MapGet("/devices/{id:int}", async (int id, MasterCrmDbContext db) =>
{
    var device = await db.Devices
        .Include(d => d.Company)
        .FirstOrDefaultAsync(d => d.DeviceId == id);
    return device is null ? Results.NotFound() : Results.Ok(device);
});

// ── Company Databases (tenant registry) ──
app.MapPost("/company-databases", async (CompanyDatabase companyDatabase, MasterCrmDbContext db) =>
{
    db.CompanyDatabases.Add(companyDatabase);
    await db.SaveChangesAsync();
    return Results.Created(
        $"/company-databases/{companyDatabase.CompanyDatabaseId}",
        companyDatabase);
});

app.MapGet("/company-databases", async (MasterCrmDbContext db) =>
{
    var dbs = await db.CompanyDatabases
        .AsNoTracking()
        .ToListAsync();
    return Results.Ok(dbs);
});

// ═══════════════════════════════════════════════════════════
// TENANT DB ENDPOINTS (factory-based, per company)
// ═══════════════════════════════════════════════════════════

app.MapGet("/test-tenant/{companyId:int}", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var repairCount = await tenantDb.RepairRequests.CountAsync();

    return Results.Ok(new
    {
        companyId,
        repairCount
    });
});

app.MapPost("/tenant/{companyId:int}/repair-requests", async (
    int companyId,
    RepairRequest repairRequest,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    // Auto-generate RequestNumber if not supplied
    if (string.IsNullOrWhiteSpace(repairRequest.RequestNumber))
    {
        repairRequest.RequestNumber =
            $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6]}";
    }

    tenantDb.RepairRequests.Add(repairRequest);
    await tenantDb.SaveChangesAsync();

    return Results.Created(
        $"/tenant/{companyId}/repair-requests/{repairRequest.RepairRequestId}",
        repairRequest);
});

app.MapGet("/tenant/{companyId:int}/repair-requests", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var repairRequests = await tenantDb.RepairRequests
        .AsNoTracking()
        .OrderBy(x => x.RepairRequestId)
        .ToListAsync();

    return Results.Ok(repairRequests);
});

app.Run();