using CRM_ComputerRepair.domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.infrastructure.Data;

public class MasterCrmDbContext : IdentityDbContext<User>
{
    public MasterCrmDbContext(DbContextOptions<MasterCrmDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyDatabase> CompanyDatabases => Set<CompanyDatabase>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<CustomerLoyaltyAccount> CustomerLoyaltyAccounts => Set<CustomerLoyaltyAccount>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<RepairRequest> RepairRequests => Set<RepairRequest>();
    public DbSet<RepairStatusHistory> RepairStatusHistories => Set<RepairStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TermsAndConditions> TermsAndConditionsSet => Set<TermsAndConditions>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>(entity =>
        {
            entity.HasKey(x => x.CompanyId);
            entity.Property(x => x.CompanyCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.CompanyCode).IsUnique();
        });

        builder.Entity<CompanyDatabase>(entity =>
        {
            entity.HasKey(x => x.CompanyDatabaseId);
            entity.Property(x => x.ServerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DatabaseName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CredentialKey).HasMaxLength(100);
            entity.HasOne(x => x.Company)
                .WithMany(c => c.CompanyDatabases)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Device>(entity =>
        {
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.DeviceCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DeviceType).HasMaxLength(100);
            entity.Property(x => x.Brand).HasMaxLength(100);
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.SerialNumber).HasMaxLength(100);
            entity.Property(x => x.WarrantyStatus).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);

            entity.HasOne(x => x.Company)
                .WithMany(c => c.Devices)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.CompanyId, x.DeviceCode }).IsUnique();
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Address).HasMaxLength(500);
        });

        builder.Entity<CustomerInteraction>(entity =>
        {
            entity.HasKey(x => x.CustomerInteractionId);
            entity.Property(x => x.Notes).HasMaxLength(1000);

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.CustomerInteractions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RepairRequest)
                .WithMany(r => r.CustomerInteractions)
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RepairRequest>(entity =>
        {
            entity.HasKey(x => x.RepairRequestId);
            entity.Property(x => x.RequestNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DeviceModel).HasMaxLength(200);
            entity.Property(x => x.SerialNumber).HasMaxLength(100);
            entity.Property(x => x.IssueDescription).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.TechnicianNotes).HasMaxLength(2000);
            entity.Property(x => x.EstimatedCost).HasPrecision(18, 2);
            entity.Property(x => x.ActualCost).HasPrecision(18, 2);
            entity.Property(x => x.PartsCost).HasPrecision(18, 2);
            entity.Property(x => x.LaborCost).HasPrecision(18, 2);
            entity.HasIndex(x => x.RequestNumber).IsUnique();

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.RepairRequests)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Device)
                .WithMany(d => d.RepairRequests)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RepairStatusHistory>(entity =>
        {
            entity.HasKey(x => x.RepairStatusHistoryId);
            entity.Property(x => x.Notes).HasMaxLength(500);

            entity.HasOne(x => x.RepairRequest)
                .WithMany()
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.PaymentId);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(50);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);

            entity.HasOne(x => x.RepairRequest)
                .WithMany()
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.ProductId);
            entity.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.ProductCode).IsUnique();
        });

        builder.Entity<LoyaltyProgram>(entity =>
        {
            entity.HasKey(x => x.LoyaltyProgramId);
            entity.Property(x => x.ProgramName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(x => x.MinimumSpend).HasPrecision(18, 2);

            entity.HasOne(x => x.Company)
                .WithMany(c => c.LoyaltyPrograms)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerLoyaltyAccount>(entity =>
        {
            entity.HasKey(x => x.CustomerLoyaltyAccountId);
            entity.Property(x => x.TotalSpent).HasPrecision(18, 2);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LoyaltyProgram)
                .WithMany()
                .HasForeignKey(x => x.LoyaltyProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Subscription>(entity =>
        {
            entity.HasKey(x => x.SubscriptionId);
            entity.Property(x => x.SubscriptionName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PricePerMonth).HasPrecision(18, 2);
            entity.Property(x => x.BillingCycle).HasMaxLength(50);

            entity.HasOne(x => x.Company)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TermsAndConditions>(entity =>
        {
            entity.HasKey(x => x.TermsId);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).IsRequired();
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.AuditLogId);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(2000);
            entity.Property(x => x.UserId).HasMaxLength(450);
        });
    }
}