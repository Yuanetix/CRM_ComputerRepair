using System.Text;
using CRM_ComputerRepair.api.Middleware;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ─── DbContexts ───
builder.Services.AddDbContext<MasterCrmDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterCrm"),
        sql => sql.EnableRetryOnFailure()));

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
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MasterCrmDbContext>()
    .AddDefaultTokenProviders();

// ─── JWT authentication ───
var jwtSection = builder.Configuration.GetSection("Jwt");
var rawKey = jwtSection["Key"] ?? "Fixory-CRM-Signing-Key-Change-Me-2026-9F4C2B71DE73AA91E0F75";
if (Encoding.UTF8.GetByteCount(rawKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 256 bits (32 bytes) long.");
}
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rawKey));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Allows both HTTP (5213) and HTTPS (7042) in development
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "Fixory",
            ValidAudience = jwtSection["Audience"] ?? "FixoryClient",
            IssuerSigningKey = signingKey,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });

// ─── Authorization Policies (using AddAuthorizationBuilder) ───
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SuperAdminOnly", policy => policy.RequireRole("Super Admin"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "Super Admin"))
    .AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager", "Admin", "Super Admin"))
    .AddPolicy("StaffOnly", policy => policy.RequireRole("Staff", "Manager", "Admin", "Super Admin"));

// ─── CORS ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─── Routing ───
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// ─── Tenant services ───
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

// ─── Services ───
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IEmailDeliveryService, SmtpEmailDeliveryService>();
builder.Services.AddScoped<RetentionEngine>();
builder.Services.AddHttpContextAccessor();

// ─── Controllers ───
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// ─── OpenAPI ───
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// ─── Apply migrations + seed data (idempotent) ───
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database seeding failed at startup.");
    }
}

// ─── Global exception handler ───
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ─── CORS (Must be before Routing & Auth) ───
app.UseCors("AllowAll");

// ─── HTTPS Redirection (Production only to avoid local dev 307 redirect issues) ───
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ─── OpenAPI spec endpoint ───
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ─── Root & Health check endpoints ───
app.MapGet("/", () => Results.Ok(new
{
    status = "Online",
    service = "Fixory CRM Computer Repair API",
    version = "1.0.0",
    environment = app.Environment.EnvironmentName,
    endpoints = new
    {
        health = "/health",
        openapi = "/openapi/v1.json"
    },
    timestamp = DateTime.UtcNow
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Fixory CRM Computer Repair API",
    timestamp = DateTime.UtcNow
}));

app.MapControllers();

app.Run();