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
    var customerCount = await tenantDb.Customers.CountAsync();
    var followUpCount = await tenantDb.FollowUps.CountAsync();
    var interactionCount = await tenantDb.CustomerInteractions.CountAsync();
    return Results.Ok(new
    {
        companyId,
        repairCount,
        customerCount,
        followUpCount,
        interactionCount
    });
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

// READ ALL
app.MapGet("/tenant/{companyId:int}/customers", async (
    int companyId,
    bool? includeArchived,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var query = tenantDb.Customers.AsNoTracking();

    if (includeArchived != true)
        query = query.Where(c => c.IsActive);

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

    if (customer is null) return Results.NotFound();

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

    if (customer is null) return Results.NotFound();

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

    if (customer is null) return Results.NotFound();

    customer.IsActive = false;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Customer {customerId} archived.",
        customer.CustomerId,
        customer.IsActive
    });
});

// RESTORE
app.MapPost("/tenant/{companyId:int}/customers/{customerId:int}/restore", async (
    int companyId,
    int customerId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customer = await tenantDb.Customers
        .FirstOrDefaultAsync(c => c.CustomerId == customerId);

    if (customer is null) return Results.NotFound();

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
// CUSTOMER INTERACTIONS — Inquiry / Complaint / Feedback
// Type: 0 = Inquiry | 1 = Complaint | 2 = Feedback
// Status: 0 = Open | 1 = InProgress | 2 = Closed
// ═══════════════════════════════════════════════════════════

// CREATE
app.MapPost("/tenant/{companyId:int}/interactions", async (
    int companyId,
    CustomerInteraction interaction,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    interaction.InteractionDate = DateTime.UtcNow;
    interaction.Status = InteractionStatus.Open;
    interaction.IsActive = true;
    interaction.UpdatedAt = null;
    interaction.ClosedAt = null;

    tenantDb.CustomerInteractions.Add(interaction);
    await tenantDb.SaveChangesAsync();

    return Results.Created(
        $"/tenant/{companyId}/interactions/{interaction.CustomerInteractionId}",
        new
        {
            interaction.CustomerInteractionId,
            interaction.CustomerId,
            interaction.RepairRequestId,
            interaction.InteractionType,
            interaction.Status,
            interaction.Priority,
            interaction.Subject,
            interaction.Notes,
            interaction.Resolution,
            interaction.InteractionByUserId,
            interaction.InteractionDate,
            interaction.UpdatedAt,
            interaction.ClosedAt,
            interaction.IsActive
        });
});

// READ ALL — filter by type + includeArchived
app.MapGet("/tenant/{companyId:int}/interactions", async (
    int companyId,
    InteractionType? type,
    bool? includeArchived,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var query = tenantDb.CustomerInteractions.AsNoTracking();

    if (includeArchived != true)
        query = query.Where(x => x.IsActive);

    if (type.HasValue)
        query = query.Where(x => x.InteractionType == type.Value);

    var list = await query
        .OrderByDescending(x => x.InteractionDate)
        .Select(x => new
        {
            x.CustomerInteractionId,
            x.CustomerId,
            x.RepairRequestId,
            x.InteractionType,
            x.Status,
            x.Priority,
            x.Subject,
            x.Notes,
            x.Resolution,
            x.InteractionByUserId,
            x.InteractionDate,
            x.UpdatedAt,
            x.ClosedAt,
            x.IsActive
        })
        .ToListAsync();

    return Results.Ok(list);
});

// READ ONE
app.MapGet("/tenant/{companyId:int}/interactions/{interactionId:int}", async (
    int companyId,
    int interactionId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.CustomerInteractions
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

    if (item is null) return Results.NotFound();

    return Results.Ok(new
    {
        item.CustomerInteractionId,
        item.CustomerId,
        item.RepairRequestId,
        item.InteractionType,
        item.Status,
        item.Priority,
        item.Subject,
        item.Notes,
        item.Resolution,
        item.InteractionByUserId,
        item.InteractionDate,
        item.UpdatedAt,
        item.ClosedAt,
        item.IsActive
    });
});

// UPDATE
app.MapPut("/tenant/{companyId:int}/interactions/{interactionId:int}", async (
    int companyId,
    int interactionId,
    CustomerInteraction updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.CustomerInteractions
        .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

    if (item is null) return Results.NotFound();

    item.CustomerId = updated.CustomerId;
    item.RepairRequestId = updated.RepairRequestId;
    item.InteractionType = updated.InteractionType;
    item.Subject = updated.Subject;
    item.Notes = updated.Notes;
    item.Priority = updated.Priority;
    item.Status = updated.Status;
    item.Resolution = updated.Resolution;
    item.UpdatedAt = DateTime.UtcNow;

    if (item.Status == InteractionStatus.Closed && item.ClosedAt is null)
        item.ClosedAt = DateTime.UtcNow;

    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        item.CustomerInteractionId,
        item.CustomerId,
        item.RepairRequestId,
        item.InteractionType,
        item.Status,
        item.Priority,
        item.Subject,
        item.Notes,
        item.Resolution,
        item.InteractionByUserId,
        item.InteractionDate,
        item.UpdatedAt,
        item.ClosedAt,
        item.IsActive
    });
});

// ARCHIVE (soft delete)
app.MapDelete("/tenant/{companyId:int}/interactions/{interactionId:int}", async (
    int companyId,
    int interactionId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.CustomerInteractions
        .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

    if (item is null) return Results.NotFound();

    item.IsActive = false;
    item.UpdatedAt = DateTime.UtcNow;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Interaction {interactionId} archived.",
        item.CustomerInteractionId,
        item.IsActive
    });
});

// RESTORE
app.MapPost("/tenant/{companyId:int}/interactions/{interactionId:int}/restore", async (
    int companyId,
    int interactionId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.CustomerInteractions
        .FirstOrDefaultAsync(x => x.CustomerInteractionId == interactionId);

    if (item is null) return Results.NotFound();

    item.IsActive = true;
    item.UpdatedAt = DateTime.UtcNow;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Interaction {interactionId} restored.",
        item.CustomerInteractionId,
        item.IsActive
    });
});

// ═══════════════════════════════════════════════════════════
// FOLLOW-UPS — Full CRUD
// Status: 0 = Scheduled | 1 = Completed | 2 = Cancelled
// Channel: 0 = Call | 1 = Email | 2 = SMS | 3 = Visit
// ═══════════════════════════════════════════════════════════

// CREATE
app.MapPost("/tenant/{companyId:int}/follow-ups", async (
    int companyId,
    FollowUp followUp,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    followUp.CreatedAt = DateTime.UtcNow;
    followUp.IsActive = true;
    followUp.UpdatedAt = null;
    followUp.CompletedAt = null;

    if (followUp.Status == FollowUpStatus.Completed && followUp.CompletedAt is null)
        followUp.CompletedAt = DateTime.UtcNow;

    tenantDb.FollowUps.Add(followUp);
    await tenantDb.SaveChangesAsync();

    return Results.Created(
        $"/tenant/{companyId}/follow-ups/{followUp.FollowUpId}",
        new
        {
            followUp.FollowUpId,
            followUp.CustomerId,
            followUp.RepairRequestId,
            followUp.Subject,
            followUp.Notes,
            followUp.ScheduledAt,
            followUp.CompletedAt,
            followUp.Channel,
            followUp.Status,
            followUp.AssignedToUserId,
            followUp.CreatedAt,
            followUp.UpdatedAt,
            followUp.IsActive
        });
});

// READ ALL — filters: status, includeArchived
app.MapGet("/tenant/{companyId:int}/follow-ups", async (
    int companyId,
    FollowUpStatus? status,
    bool? includeArchived,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var query = tenantDb.FollowUps.AsNoTracking();

    if (includeArchived != true)
        query = query.Where(x => x.IsActive);

    if (status.HasValue)
        query = query.Where(x => x.Status == status.Value);

    var list = await query
        .OrderBy(x => x.ScheduledAt)
        .Select(x => new
        {
            x.FollowUpId,
            x.CustomerId,
            x.RepairRequestId,
            x.Subject,
            x.Notes,
            x.ScheduledAt,
            x.CompletedAt,
            x.Channel,
            x.Status,
            x.AssignedToUserId,
            x.CreatedAt,
            x.UpdatedAt,
            x.IsActive
        })
        .ToListAsync();

    return Results.Ok(list);
});

// READ ONE
app.MapGet("/tenant/{companyId:int}/follow-ups/{followUpId:int}", async (
    int companyId,
    int followUpId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.FollowUps
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

    if (item is null) return Results.NotFound();

    return Results.Ok(new
    {
        item.FollowUpId,
        item.CustomerId,
        item.RepairRequestId,
        item.Subject,
        item.Notes,
        item.ScheduledAt,
        item.CompletedAt,
        item.Channel,
        item.Status,
        item.AssignedToUserId,
        item.CreatedAt,
        item.UpdatedAt,
        item.IsActive
    });
});

// UPDATE
app.MapPut("/tenant/{companyId:int}/follow-ups/{followUpId:int}", async (
    int companyId,
    int followUpId,
    FollowUp updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.FollowUps
        .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

    if (item is null) return Results.NotFound();

    item.CustomerId = updated.CustomerId;
    item.RepairRequestId = updated.RepairRequestId;
    item.Subject = updated.Subject;
    item.Notes = updated.Notes;
    item.ScheduledAt = updated.ScheduledAt;
    item.Channel = updated.Channel;
    item.Status = updated.Status;
    item.AssignedToUserId = updated.AssignedToUserId;
    item.UpdatedAt = DateTime.UtcNow;

    // Set CompletedAt when marked Completed
    if (item.Status == FollowUpStatus.Completed && item.CompletedAt is null)
        item.CompletedAt = DateTime.UtcNow;

    // Clear CompletedAt when un-completed
    if (item.Status != FollowUpStatus.Completed)
        item.CompletedAt = null;

    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        item.FollowUpId,
        item.CustomerId,
        item.RepairRequestId,
        item.Subject,
        item.Notes,
        item.ScheduledAt,
        item.CompletedAt,
        item.Channel,
        item.Status,
        item.AssignedToUserId,
        item.CreatedAt,
        item.UpdatedAt,
        item.IsActive
    });
});

// ARCHIVE (soft delete)
app.MapDelete("/tenant/{companyId:int}/follow-ups/{followUpId:int}", async (
    int companyId,
    int followUpId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.FollowUps
        .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

    if (item is null) return Results.NotFound();

    item.IsActive = false;
    item.UpdatedAt = DateTime.UtcNow;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Follow-up {followUpId} archived.",
        item.FollowUpId,
        item.IsActive
    });
});

// RESTORE
app.MapPost("/tenant/{companyId:int}/follow-ups/{followUpId:int}/restore", async (
    int companyId,
    int followUpId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.FollowUps
        .FirstOrDefaultAsync(x => x.FollowUpId == followUpId);

    if (item is null) return Results.NotFound();

    item.IsActive = true;
    item.UpdatedAt = DateTime.UtcNow;
    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        message = $"Follow-up {followUpId} restored.",
        item.FollowUpId,
        item.IsActive
    });
});

// ═══════════════════════════════════════════════════════════
// REPAIR REQUESTS — Full CRUD with status workflow
// Status: 0 = Pending | 1 = Approved | 2 = InProgress
//         3 = Completed | 4 = Rejected | 5 = Reassigned
// Priority: 0 = Low | 1 = Medium | 2 = High | 3 = Urgent
// ═══════════════════════════════════════════════════════════

// CREATE
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

    repairRequest.RequestDate = DateTime.UtcNow;
    repairRequest.Status = RepairStatus.Pending;

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
            repairRequest.CompletionDate,
            repairRequest.EstimatedCost,
            repairRequest.ActualCost,
            repairRequest.PartsCost,
            repairRequest.LaborCost,
            repairRequest.TechnicianNotes,
            repairRequest.AssignedToStaffId,
            repairRequest.AssignedToManagerId
        });
});

// READ ALL
app.MapGet("/tenant/{companyId:int}/repair-requests", async (
    int companyId,
    RepairStatus? status,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var query = tenantDb.RepairRequests.AsNoTracking();

    if (status.HasValue)
        query = query.Where(x => x.Status == status.Value);

    var list = await query
        .OrderByDescending(x => x.RequestDate)
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
            x.CompletionDate,
            x.EstimatedCost,
            x.ActualCost,
            x.PartsCost,
            x.LaborCost,
            x.TechnicianNotes,
            x.AssignedToStaffId,
            x.AssignedToManagerId
        })
        .ToListAsync();

    return Results.Ok(list);
});

// READ ONE
app.MapGet("/tenant/{companyId:int}/repair-requests/{repairRequestId:int}", async (
    int companyId,
    int repairRequestId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.RepairRequests
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

    if (item is null) return Results.NotFound();

    return Results.Ok(new
    {
        item.RepairRequestId,
        item.RequestNumber,
        item.CustomerId,
        item.DeviceId,
        item.DeviceModel,
        item.SerialNumber,
        item.IssueDescription,
        item.Status,
        item.Priority,
        item.RequestDate,
        item.CompletionDate,
        item.EstimatedCost,
        item.ActualCost,
        item.PartsCost,
        item.LaborCost,
        item.TechnicianNotes,
        item.AssignedToStaffId,
        item.AssignedToManagerId
    });
});

// UPDATE
app.MapPut("/tenant/{companyId:int}/repair-requests/{repairRequestId:int}", async (
    int companyId,
    int repairRequestId,
    RepairRequest updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var item = await tenantDb.RepairRequests
        .FirstOrDefaultAsync(x => x.RepairRequestId == repairRequestId);

    if (item is null) return Results.NotFound();

    item.CustomerId = updated.CustomerId;
    item.DeviceId = updated.DeviceId;
    item.DeviceModel = updated.DeviceModel;
    item.SerialNumber = updated.SerialNumber;
    item.IssueDescription = updated.IssueDescription;
    item.Priority = updated.Priority;
    item.Status = updated.Status;
    item.EstimatedCost = updated.EstimatedCost;
    item.ActualCost = updated.ActualCost;
    item.PartsCost = updated.PartsCost;
    item.LaborCost = updated.LaborCost;
    item.TechnicianNotes = updated.TechnicianNotes;
    item.AssignedToStaffId = updated.AssignedToStaffId;
    item.AssignedToManagerId = updated.AssignedToManagerId;

    if (item.Status == RepairStatus.Completed && item.CompletionDate is null)
        item.CompletionDate = DateTime.UtcNow;

    await tenantDb.SaveChangesAsync();

    return Results.Ok(new
    {
        item.RepairRequestId,
        item.RequestNumber,
        item.CustomerId,
        item.DeviceId,
        item.DeviceModel,
        item.SerialNumber,
        item.IssueDescription,
        item.Status,
        item.Priority,
        item.RequestDate,
        item.CompletionDate,
        item.EstimatedCost,
        item.ActualCost,
        item.PartsCost,
        item.LaborCost,
        item.TechnicianNotes,
        item.AssignedToStaffId,
        item.AssignedToManagerId
    });
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