using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM_ComputerRepair.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsVoidToMasterPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoid",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxInactiveDays",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRedemptionsPerCustomer",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinTotalSpent",
                table: "LoyaltyPrograms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinTransactions",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinVisitsPerPeriod",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsValidityDays",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RedeemPointsRequired",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RewardType",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RewardValue",
                table: "LoyaltyPrograms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VisitPeriodDays",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVoid",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaxInactiveDays",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "MaxRedemptionsPerCustomer",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "MinTotalSpent",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "MinTransactions",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "MinVisitsPerPeriod",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "PointsValidityDays",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "RedeemPointsRequired",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "RewardType",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "RewardValue",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "VisitPeriodDays",
                table: "LoyaltyPrograms");
        }
    }
}
