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

// ── Controllers + JSON cycle handling ──
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

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
    var companies = await db.Companies.Include(c => c.Devices).ToListAsync();
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
    var devices = await db.Devices.Include(d => d.Company).ToListAsync();
    return Results.Ok(devices);
});

app.MapGet("/devices/{id:int}", async (int id, MasterCrmDbContext db) =>
{
    var device = await db.Devices
        .Include(d => d.Company)
        .FirstOrDefaultAsync(d => d.DeviceId == id);
    return device is null ? Results.NotFound() : Results.Ok(device);
});

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
    var dbs = await db.CompanyDatabases.AsNoTracking().ToListAsync();
    return Results.Ok(dbs);
});

// ═══════════════════════════════════════════════════════════
// TENANT — TEST
// ═══════════════════════════════════════════════════════════

app.MapGet("/test-tenant/{companyId:int}", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var repairCount = await tenantDb.RepairRequests.CountAsync();
    return Results.Ok(new { companyId, repairCount });
});

// ═══════════════════════════════════════════════════════════
// CUSTOMERS — Full CRUD with Archive (soft delete)
// ═══════════════════════════════════════════════════════════

// CREATE
app.MapPost("/tenant/{companyId:int}/customers", async (
    int companyId,
    Customer customer,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    customer.IsActive = true;
    customer.CreatedAt = DateTime.UtcNow;

    tenantDb.Customers.Add(customer);
    await tenantDb.SaveChangesAsync();

    return Results.Created(
        $"/tenant/{companyId}/customers/{customer.CustomerId}",
        new
        {
            customer.CustomerId,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.LoyaltyPoints,
            customer.IsActive,
            customer.CreatedAt
        });
});

// READ ALL (active by default; includeArchived=true to see all)
app.MapGet("/tenant/{companyId:int}/customers", async (
    int companyId,
    bool? includeArchived,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var query = tenantDb.Customers.AsNoTracking();

    if (includeArchived != true)
    {
        query = query.Where(c => c.IsActive);
    }

    var customers = await query
        .OrderBy(x => x.CustomerId)
        .Select(x => new
        {
            x.CustomerId,
            x.FirstName,
            x.LastName,
            x.Email,
            x.Phone,
            x.Address,
            x.LoyaltyPoints,
            x.IsActive,
            x.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(customers);
});

// READ ONE
app.MapGet("/tenant/{companyId:int}/customers/{customerId:int}", async (
    int companyId,
    int customerId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customer = await tenantDb.Customers
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.CustomerId == customerId);

    if (customer is null)
        return Results.NotFound();

    return Results.Ok(new
    {
        customer.CustomerId,
        customer.FirstName,
        customer.LastName,
        customer.Email,
        customer.Phone,
        customer.Address,
        customer.LoyaltyPoints,
        customer.IsActive,
        customer.CreatedAt
    });
});

// UPDATE
app.MapPut("/tenant/{companyId:int}/customers/{customerId:int}", async (
    int companyId,
    int customerId,
    Customer updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customer = await tenantDb.Customers
        .FirstOrDefaultAsync(c => c.CustomerId == customerId);

    if (customer is null)
        return Results.NotFound();

    customer.FirstName = updated.FirstName;
    customer.LastName = updated.LastName;
    customer.Email = updated.Email;
    customer.Phone = updated.Phone;
    customer.Address = updated.Address;

    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        customer.CustomerId,
        customer.FirstName,
        customer.LastName,
        customer.Email,
        customer.Phone,
        customer.Address,
        customer.LoyaltyPoints,
        customer.IsActive,
        customer.CreatedAt
    });
});

// ARCHIVE (soft delete)
app.MapDelete("/tenant/{companyId:int}/customers/{customerId:int}", async (
    int companyId,
    int customerId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customer = await tenantDb.Customers
        .FirstOrDefaultAsync(c => c.CustomerId == customerId);

    if (customer is null)
        return Results.NotFound();

    customer.IsActive = false;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Customer {customerId} archived.",
        customer.CustomerId,
        customer.IsActive
    });
});

// UNARCHIVE (restore)
app.MapPost("/tenant/{companyId:int}/customers/{customerId:int}/restore", async (
    int companyId,
    int customerId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customer = await tenantDb.Customers
        .FirstOrDefaultAsync(c => c.CustomerId == customerId);

    if (customer is null)
        return Results.NotFound();

    customer.IsActive = true;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Customer {customerId} restored.",
        customer.CustomerId,
        customer.IsActive
    });
});

// ═══════════════════════════════════════════════════════════
// REPAIR REQUESTS
// ═══════════════════════════════════════════════════════════

app.MapPost("/tenant/{companyId:int}/repair-requests", async (
    int companyId,
    RepairRequest repairRequest,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    if (string.IsNullOrWhiteSpace(repairRequest.RequestNumber))
    {
        repairRequest.RequestNumber =
            $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6]}";
    }

    tenantDb.RepairRequests.Add(repairRequest);
    await tenantDb.SaveChangesAsync();

    return Results.Created(
        $"/tenant/{companyId}/repair-requests/{repairRequest.RepairRequestId}",
        new
        {
            repairRequest.RepairRequestId,
            repairRequest.RequestNumber,
            repairRequest.CustomerId,
            repairRequest.DeviceId,
            repairRequest.DeviceModel,
            repairRequest.SerialNumber,
            repairRequest.IssueDescription,
            repairRequest.Status,
            repairRequest.Priority,
            repairRequest.RequestDate,
            repairRequest.EstimatedCost
        });
});

app.MapGet("/tenant/{companyId:int}/repair-requests", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var repairRequests = await tenantDb.RepairRequests
        .AsNoTracking()
        .OrderBy(x => x.RepairRequestId)
        .Select(x => new
        {
            x.RepairRequestId,
            x.RequestNumber,
            x.CustomerId,
            x.DeviceId,
            x.DeviceModel,
            x.SerialNumber,
            x.IssueDescription,
            x.Status,
            x.Priority,
            x.RequestDate,
            x.EstimatedCost
        })
        .ToListAsync();
    return Results.Ok(repairRequests);
});

// ═══════════════════════════════════════════════════════════
// SUPPLIERS
// ═══════════════════════════════════════════════════════════

app.MapPost("/tenant/{companyId:int}/suppliers", async (
    int companyId,
    Supplier supplier,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    tenantDb.Suppliers.Add(supplier);
    await tenantDb.SaveChangesAsync();
    return Results.Created(
        $"/tenant/{companyId}/suppliers/{supplier.SupplierId}",
        new
        {
            supplier.SupplierId,
            supplier.SupplierCode,
            supplier.SupplierName,
            supplier.ContactPerson,
            supplier.ContactNumber,
            supplier.EmailAddress,
            supplier.Address,
            supplier.Notes,
            supplier.IsActive,
            supplier.CreatedAt
        });
});

app.MapGet("/tenant/{companyId:int}/suppliers", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var suppliers = await tenantDb.Suppliers
        .AsNoTracking()
        .OrderBy(x => x.SupplierId)
        .Select(x => new
        {
            x.SupplierId,
            x.SupplierCode,
            x.SupplierName,
            x.ContactPerson,
            x.ContactNumber,
            x.EmailAddress,
            x.Address,
            x.Notes,
            x.IsActive,
            x.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(suppliers);
});

// ═══════════════════════════════════════════════════════════
// PARTS
// ═══════════════════════════════════════════════════════════

app.MapPost("/tenant/{companyId:int}/parts", async (
    int companyId,
    Part part,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    tenantDb.Parts.Add(part);
    await tenantDb.SaveChangesAsync();
    return Results.Created(
        $"/tenant/{companyId}/parts/{part.PartId}",
        new
        {
            part.PartId,
            part.PartCode,
            part.PartName,
            part.Category,
            part.Manufacturer,
            part.Model,
            part.UnitCost,
            part.UnitPrice,
            part.QuantityOnHand,
            part.ReorderLevel,
            part.SupplierId,
            part.IsActive,
            part.CreatedAt
        });
});

app.MapGet("/tenant/{companyId:int}/parts", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var parts = await tenantDb.Parts
        .AsNoTracking()
        .OrderBy(x => x.PartId)
        .Select(x => new
        {
            x.PartId,
            x.PartCode,
            x.PartName,
            x.Category,
            x.Manufacturer,
            x.Model,
            x.UnitCost,
            x.UnitPrice,
            x.QuantityOnHand,
            x.ReorderLevel,
            x.SupplierId,
            x.IsActive,
            x.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(parts);
});

// ═══════════════════════════════════════════════════════════
// REPAIR PARTS
// ═══════════════════════════════════════════════════════════

app.MapPost("/tenant/{companyId:int}/repair-requests/{repairRequestId:int}/parts", async (
    int companyId,
    int repairRequestId,
    RepairPart repairPart,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var repair = await tenantDb.RepairRequests
        .FirstOrDefaultAsync(r => r.RepairRequestId == repairRequestId);

    if (repair is null)
        return Results.NotFound($"RepairRequest {repairRequestId} not found.");

    repairPart.RepairRequestId = repairRequestId;

    var part = await tenantDb.Parts
        .FirstOrDefaultAsync(p => p.PartId == repairPart.PartId);

    if (part is null)
        return Results.NotFound($"Part {repairPart.PartId} not found.");

    repairPart.UnitCostAtTime = part.UnitCost;
    repairPart.UnitPriceAtTime = part.UnitPrice;

    tenantDb.RepairParts.Add(repairPart);

    part.QuantityOnHand -= repairPart.QuantityUsed;
    part.UpdatedAt = DateTime.UtcNow;

    await tenantDb.SaveChangesAsync();

    return Results.Created(
        $"/tenant/{companyId}/repair-requests/{repairRequestId}/parts/{repairPart.RepairPartId}",
        new
        {
            repairPart.RepairPartId,
            repairPart.RepairRequestId,
            repairPart.PartId,
            repairPart.QuantityUsed,
            repairPart.UnitCostAtTime,
            repairPart.UnitPriceAtTime,
            repairPart.UsedAt
        });
});

app.MapGet("/tenant/{companyId:int}/repair-requests/{repairRequestId:int}/parts", async (
    int companyId,
    int repairRequestId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var repairParts = await tenantDb.RepairParts
        .AsNoTracking()
        .Where(rp => rp.RepairRequestId == repairRequestId)
        .OrderBy(rp => rp.RepairPartId)
        .Select(rp => new
        {
            rp.RepairPartId,
            rp.RepairRequestId,
            rp.PartId,
            rp.QuantityUsed,
            rp.UnitCostAtTime,
            rp.UnitPriceAtTime,
            rp.UsedAt
        })
        .ToListAsync();
    return Results.Ok(repairParts);
});

app.Run();