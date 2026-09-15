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

    // Lab 5 additions
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<RepairPart> RepairParts => Set<RepairPart>();

    // Step 1 addition
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();

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

        // ═══════════ CustomerInteraction (Inquiry / Complaint / Feedback) ═══════════
        builder.Entity<CustomerInteraction>(entity =>
        {
            entity.HasKey(x => x.CustomerInteractionId);

            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Resolution).HasMaxLength(2000);
            entity.Property(x => x.InteractionByUserId).HasMaxLength(450);

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.CustomerInteractions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            entity.HasOne(x => x.RepairRequest)
                .WithMany(r => r.CustomerInteractions)
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════ FollowUp (NEW) ═══════════
        builder.Entity<FollowUp>(entity =>
        {
            entity.HasKey(x => x.FollowUpId);

            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.AssignedToUserId).HasMaxLength(450);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            entity.HasOne(x => x.RepairRequest)
                .WithMany()
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
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

        // ═══════════ Supplier (Lab 5) ═══════════
        builder.Entity<Supplier>(entity =>
        {
            entity.HasKey(x => x.SupplierId);
            entity.Property(x => x.SupplierCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactPerson).HasMaxLength(100);
            entity.Property(x => x.ContactNumber).HasMaxLength(50);
            entity.Property(x => x.EmailAddress).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => x.SupplierCode).IsUnique();
        });

        // ═══════════ Part (Lab 5) ═══════════
        builder.Entity<Part>(entity =>
        {
            entity.HasKey(x => x.PartId);
            entity.Property(x => x.PartCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PartName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.Manufacturer).HasMaxLength(100);
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.PartCode).IsUnique();

            entity.HasOne(x => x.Supplier)
                .WithMany(s => s.Parts)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════ RepairPart (Lab 5) ═══════════
        builder.Entity<RepairPart>(entity =>
        {
            entity.HasKey(x => x.RepairPartId);
            entity.Property(x => x.UnitCostAtTime).HasPrecision(18, 2);
            entity.Property(x => x.UnitPriceAtTime).HasPrecision(18, 2);

            entity.HasOne(x => x.RepairRequest)
                .WithMany(r => r.RepairParts)
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Part)
                .WithMany(p => p.RepairParts)
                .HasForeignKey(x => x.PartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════════════════════
        // MASTER-ONLY ENTITIES — should NOT exist in the tenant DB.
        // ═══════════════════════════════════════════════════════════
        builder.Ignore<Company>();
        builder.Ignore<CompanyDatabase>();
        builder.Ignore<LoyaltyProgram>();
        builder.Ignore<Subscription>();
        builder.Ignore<TermsAndConditions>();
        builder.Ignore<AuditLog>();
        builder.Ignore<CustomerLoyaltyAccount>();
        // NOTE: Product was deleted from the project entirely.
    }
}