using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM_ComputerRepair.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddRetentionAndEmailCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetentionEmailTemplates",
                columns: table => new
                {
                    RetentionEmailTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Segment = table.Column<int>(type: "int", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionEmailTemplates", x => x.RetentionEmailTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "RetentionRequests",
                columns: table => new
                {
                    RetentionRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    TargetSegment = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProposedDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RetentionDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReasonCategory = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ReasonNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AddedToCampaign = table.Column<bool>(type: "bit", nullable: false),
                    CampaignAddedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionRequests", x => x.RetentionRequestId);
                    table.ForeignKey(
                        name: "FK_RetentionRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionSettings",
                columns: table => new
                {
                    RetentionSettingsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InactiveThresholdDays = table.Column<int>(type: "int", nullable: false),
                    AtRiskThresholdDays = table.Column<int>(type: "int", nullable: false),
                    AntiFatigueDays = table.Column<int>(type: "int", nullable: false),
                    DefaultOfferValidityDays = table.Column<int>(type: "int", nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUsername = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPassword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpFromEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpFromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpEnableSsl = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionSettings", x => x.RetentionSettingsId);
                });

            migrationBuilder.CreateTable(
                name: "RetentionEmailLogs",
                columns: table => new
                {
                    RetentionEmailLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetentionRequestId = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FormattedBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Segment = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PromoCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDispatched = table.Column<bool>(type: "bit", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsAutomated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionEmailLogs", x => x.RetentionEmailLogId);
                    table.ForeignKey(
                        name: "FK_RetentionEmailLogs_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetentionEmailLogs_RetentionRequests_RetentionRequestId",
                        column: x => x.RetentionRequestId,
                        principalTable: "RetentionRequests",
                        principalColumn: "RetentionRequestId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionEmailLogs_CustomerId",
                table: "RetentionEmailLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionEmailLogs_RetentionRequestId",
                table: "RetentionEmailLogs",
                column: "RetentionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRequests_CustomerId",
                table: "RetentionRequests",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetentionEmailLogs");

            migrationBuilder.DropTable(
                name: "RetentionEmailTemplates");

            migrationBuilder.DropTable(
                name: "RetentionSettings");

            migrationBuilder.DropTable(
                name: "RetentionRequests");
        }
    }
}
