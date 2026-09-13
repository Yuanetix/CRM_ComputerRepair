using CRM_ComputerRepair.domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.infrastructure.Data;

public class TenantCrmDbContext : DbContext
{
    public TenantCrmDbContext(DbContextOptions<TenantCrmDbContext> options)
        : base(options)
    {
    }

    // Tenant-scoped entities
    public DbSet<RepairRequest> RepairRequests => Set<RepairRequest>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<RepairStatusHistory> RepairStatusHistories => Set<RepairStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ═══════════ RepairRequest ═══════════
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
        });

        // ═══════════ Customer ═══════════
        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.CustomerId);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Address).HasMaxLength(500);
        });

        // ═══════════ Device ═══════════
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
        });

        // ═══════════ CustomerInteraction ═══════════
        builder.Entity<CustomerInteraction>(entity =>
        {
            entity.HasKey(x => x.CustomerInteractionId);
            entity.Property(x => x.Notes).HasMaxLength(1000);
        });

        // ═══════════ RepairStatusHistory ═══════════
        builder.Entity<RepairStatusHistory>(entity =>
        {
            entity.HasKey(x => x.RepairStatusHistoryId);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        // ═══════════ Payment ═══════════
        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.PaymentId);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(50);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);
        });

        // ═══════════════════════════════════════════════════════════
        // MASTER-ONLY ENTITIES — should NOT exist in the tenant DB.
        // Ignore them so EF doesn't map them transitively.
        // ═══════════════════════════════════════════════════════════
        builder.Ignore<Company>();
        builder.Ignore<CompanyDatabase>();
        builder.Ignore<LoyaltyProgram>();
        builder.Ignore<Subscription>();
        builder.Ignore<TermsAndConditions>();
        builder.Ignore<AuditLog>();
        builder.Ignore<Product>();
        builder.Ignore<CustomerLoyaltyAccount>();
    }
}