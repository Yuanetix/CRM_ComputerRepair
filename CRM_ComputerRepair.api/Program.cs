using CRM_ComputerRepair.api.Middleware;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── DbContexts ───
builder.Services.AddDbContext<MasterCrmDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterCrm")));

// ─── Data protection (required by Identity token providers) ───
builder.Services.AddDataProtection();

// ─── Identity ───
builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MasterCrmDbContext>()
    .AddDefaultTokenProviders();

// ─── Tenant services ───
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

// ─── Audit ───
builder.Services.AddScoped<IAuditWriter, AuditWriter>();

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