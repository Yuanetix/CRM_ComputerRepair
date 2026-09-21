using CRM_ComputerRepair.api.Middleware;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── DbContexts ───
builder.Services.AddDbContext<MasterCrmDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterCrm")));

// ─── Tenant services ───
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

// ─── Controllers ───
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// ─── OpenAPI ───
builder.Services.AddOpenApi();

var app = builder.Build();

// ─── Global exception handler ───
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();