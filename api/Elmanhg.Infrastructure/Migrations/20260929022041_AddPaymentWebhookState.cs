using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWebhookState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Subscriptions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<int>(
                name: "PeriodMonths",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"Payments\" SET \"PeriodMonths\" = CASE \"Period\" WHEN 'Monthly' THEN 1 WHEN 'Termly' THEN 4 ELSE 12 END;");

            migrationBuilder.AddColumn<string>(
                name: "ProviderOrderId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "Payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Payments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status_CurrentPeriodEnd",
                table: "Subscriptions",
                columns: new[] { "Status", "CurrentPeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderOrderId",
                table: "Payments",
                column: "ProviderOrderId",
                filter: "\"ProviderOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_Status_CurrentPeriodEnd",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderOrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PeriodMonths",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderOrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Payments");
        }
    }
}
