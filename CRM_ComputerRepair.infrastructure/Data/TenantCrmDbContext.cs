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

    // Retention & Email Campaigns
    public DbSet<RetentionRequest> RetentionRequests => Set<RetentionRequest>();
    public DbSet<RetentionEmailLog> RetentionEmailLogs => Set<RetentionEmailLog>();
    public DbSet<RetentionEmailTemplate> RetentionEmailTemplates => Set<RetentionEmailTemplate>();
    public DbSet<RetentionSettings> RetentionSettings => Set<RetentionSettings>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

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

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.RepairRequests)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Device)
                .WithMany(d => d.RepairRequests)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestDate);
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
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.StateOrProvince).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(100);

            entity.Ignore(x => x.FullName);
            entity.Ignore(x => x.FullAddress);

            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.Phone);
            entity.HasIndex(x => x.LastName);
            entity.HasIndex(x => x.City);
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

            entity.HasOne(x => x.Customer)
                .WithMany(c => c.Devices)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.CustomerId);
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

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.RepairRequestId);
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

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.RepairRequestId);
            entity.HasIndex(x => x.Status);
        });

        // ═══════════ RepairStatusHistory ═══════════
        builder.Entity<RepairStatusHistory>(entity =>
        {
            entity.HasKey(x => x.RepairStatusHistoryId);
            entity.Property(x => x.Notes).HasMaxLength(500);

            entity.HasOne(x => x.RepairRequest)
                .WithMany()
                .HasForeignKey(x => x.RepairRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.RepairRequestId);
        });

        // ═══════════ Payment ═══════════
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

            entity.HasIndex(x => x.RepairRequestId);
            entity.HasIndex(x => x.PaymentDate);
        });

        // ═══════════ Supplier (Lab 5) ═══════════
        builder.Entity<Supplier>(entity =>
        {
            entity.HasKey(x => x.SupplierId);
            entity.Property(x => x.SupplierCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactFirstName).HasMaxLength(100);
            entity.Property(x => x.ContactLastName).HasMaxLength(100);
            entity.Property(x => x.ContactNumber).HasMaxLength(50);
            entity.Property(x => x.EmailAddress).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.StateOrProvince).HasMaxLength(100);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => x.SupplierCode).IsUnique();

            entity.Ignore(x => x.ContactPerson);
            entity.Ignore(x => x.FullAddress);
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

            entity.HasIndex(x => x.SupplierId);
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

            entity.HasIndex(x => x.RepairRequestId);
            entity.HasIndex(x => x.PartId);
        });

        // ═══════════ RetentionRequest ═══════════
        builder.Entity<RetentionRequest>(entity =>
        {
            entity.HasKey(x => x.RetentionRequestId);
            entity.Property(x => x.ActionType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProposedDiscountPercent).HasPrecision(5, 2);
            entity.Property(x => x.RetentionDetails).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ReasonCategory).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ReasonNote).HasMaxLength(2000);
            entity.Property(x => x.SubmittedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.SubmittedByFirstName).HasMaxLength(100);
            entity.Property(x => x.SubmittedByLastName).HasMaxLength(100);
            entity.Property(x => x.ReviewedByUserId).HasMaxLength(450);
            entity.Property(x => x.ReviewedByFirstName).HasMaxLength(100);
            entity.Property(x => x.ReviewedByLastName).HasMaxLength(100);
            entity.Property(x => x.ReviewRemarks).HasMaxLength(2000);
            entity.Property(x => x.RejectionReason).HasMaxLength(2000);

            entity.Ignore(x => x.SubmittedByName);
            entity.Ignore(x => x.ReviewedByName);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Status);
        });

        // ═══════════ RetentionEmailLog ═══════════
        builder.Entity<RetentionEmailLog>(entity =>
        {
            entity.HasKey(x => x.RetentionEmailLogId);
            entity.Property(x => x.RecipientEmail).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RecipientFirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RecipientLastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.FormattedBody).IsRequired();
            entity.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            entity.Property(x => x.PromoCode).HasMaxLength(50);
            entity.Property(x => x.DispatchedByUserId).HasMaxLength(450);
            entity.Property(x => x.DeliveryStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DeliveryError).HasMaxLength(2000);

            entity.Ignore(x => x.RecipientName);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RetentionRequest)
                .WithMany(r => r.EmailLogs)
                .HasForeignKey(x => x.RetentionRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.RetentionRequestId);
            entity.HasIndex(x => x.CreatedAt);
        });

        // ═══════════ RetentionEmailTemplate ═══════════
        builder.Entity<RetentionEmailTemplate>(entity =>
        {
            entity.HasKey(x => x.RetentionEmailTemplateId);
            entity.Property(x => x.TemplateName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Body).IsRequired();
            entity.Property(x => x.DefaultDiscountPercent).HasPrecision(5, 2);
        });

        // ═══════════ RetentionSettings ═══════════
        builder.Entity<RetentionSettings>(entity =>
        {
            entity.HasKey(x => x.RetentionSettingsId);
            entity.Property(x => x.SmtpHost).HasMaxLength(200);
            entity.Property(x => x.SmtpUsername).HasMaxLength(200);
            entity.Property(x => x.SmtpPassword).HasMaxLength(200);
            entity.Property(x => x.SmtpFromEmail).HasMaxLength(200);
            entity.Property(x => x.SmtpFromName).HasMaxLength(200);
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