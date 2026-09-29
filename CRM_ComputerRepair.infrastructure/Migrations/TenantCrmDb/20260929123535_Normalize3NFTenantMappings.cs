using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM_ComputerRepair.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class Normalize3NFTenantMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_RepairRequests_RepairRequestId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Customers_CustomerId",
                table: "RepairRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Devices_DeviceId",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "RetentionRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedByName",
                table: "RetentionRequests");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "RetentionEmailLogs");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "Suppliers");

            migrationBuilder.AddColumn<string>(
                name: "StateOrProvince",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFirstName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactLastName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByFirstName",
                table: "RetentionRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByLastName",
                table: "RetentionRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByFirstName",
                table: "RetentionRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByLastName",
                table: "RetentionRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientFirstName",
                table: "RetentionEmailLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientLastName",
                table: "RetentionEmailLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateOrProvince",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionRequests_Status",
                table: "RetentionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionEmailLogs_CreatedAt",
                table: "RetentionEmailLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RepairRequests_RequestDate",
                table: "RepairRequests",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_RepairRequests_Status",
                table: "RepairRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_Status",
                table: "FollowUps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CustomerId",
                table: "Devices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_City",
                table: "Customers",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LastName",
                table: "Customers",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Customers_CustomerId",
                table: "Devices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_RepairRequests_RepairRequestId",
                table: "Payments",
                column: "RepairRequestId",
                principalTable: "RepairRequests",
                principalColumn: "RepairRequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Customers_CustomerId",
                table: "RepairRequests",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Devices_DeviceId",
                table: "RepairRequests",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "DeviceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Customers_CustomerId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_RepairRequests_RepairRequestId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Customers_CustomerId",
                table: "RepairRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Devices_DeviceId",
                table: "RepairRequests");

            migrationBuilder.DropIndex(
                name: "IX_RetentionRequests_Status",
                table: "RetentionRequests");

            migrationBuilder.DropIndex(
                name: "IX_RetentionEmailLogs_CreatedAt",
                table: "RetentionEmailLogs");

            migrationBuilder.DropIndex(
                name: "IX_RepairRequests_RequestDate",
                table: "RepairRequests");

            migrationBuilder.DropIndex(
                name: "IX_RepairRequests_Status",
                table: "RepairRequests");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_Status",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_Devices_CustomerId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Customers_City",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_LastName",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ContactFirstName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ContactLastName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ReviewedByFirstName",
                table: "RetentionRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByLastName",
                table: "RetentionRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedByFirstName",
                table: "RetentionRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedByLastName",
                table: "RetentionRequests");

            migrationBuilder.DropColumn(
                name: "RecipientFirstName",
                table: "RetentionEmailLogs");

            migrationBuilder.DropColumn(
                name: "RecipientLastName",
                table: "RetentionEmailLogs");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "StateOrProvince",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "StateOrProvince",
                table: "Suppliers");

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "RetentionRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByName",
                table: "RetentionRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "RetentionEmailLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_RepairRequests_RepairRequestId",
                table: "Payments",
                column: "RepairRequestId",
                principalTable: "RepairRequests",
                principalColumn: "RepairRequestId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Customers_CustomerId",
                table: "RepairRequests",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Devices_DeviceId",
                table: "RepairRequests",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "DeviceId");
        }
    }
}
