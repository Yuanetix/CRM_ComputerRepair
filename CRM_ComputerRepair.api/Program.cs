using Microsoft.EntityFrameworkCore;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.domain.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MasterCrmDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MasterCrm")));

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

// Company endpoints
app.MapPost("/companies", async (
    Company company,
    MasterCrmDbContext db) =>
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

    return company is null
        ? Results.NotFound()
        : Results.Ok(company);
});

// Device endpoints
app.MapPost("/devices", async (
    Device device,
    MasterCrmDbContext db) =>
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

    return device is null
        ? Results.NotFound()
        : Results.Ok(device);
});

app.Run();