using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM_ComputerRepair.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddInteractionCrudFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInteractions_Customers_CustomerId",
                table: "CustomerInteractions");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInteractions_RepairRequests_RepairRequestId",
                table: "CustomerInteractions");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "CustomerInteractions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "InteractionByUserId",
                table: "CustomerInteractions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "CustomerInteractions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "CustomerInteractions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CustomerInteractions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "CustomerInteractions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "CustomerInteractions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CustomerInteractions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "CustomerInteractions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CustomerInteractions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInteractions_Customers_CustomerId",
                table: "CustomerInteractions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInteractions_RepairRequests_RepairRequestId",
                table: "CustomerInteractions",
                column: "RepairRequestId",
                principalTable: "RepairRequests",
                principalColumn: "RepairRequestId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInteractions_Customers_CustomerId",
                table: "CustomerInteractions");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInteractions_RepairRequests_RepairRequestId",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CustomerInteractions");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "CustomerInteractions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "InteractionByUserId",
                table: "CustomerInteractions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "CustomerInteractions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInteractions_Customers_CustomerId",
                table: "CustomerInteractions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInteractions_RepairRequests_RepairRequestId",
                table: "CustomerInteractions",
                column: "RepairRequestId",
                principalTable: "RepairRequests",
                principalColumn: "RepairRequestId");
        }
    }
}
