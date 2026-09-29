using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RefundIdempotencyKey",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundTransactionId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundedBy",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewResolvedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewResolvedBy",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreationDate",
                table: "Payments",
                column: "CreationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OpenReview",
                table: "Payments",
                column: "CreationDate",
                filter: "\"ReviewReason\" IS NOT NULL AND \"ReviewResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RefundTransactionId",
                table: "Payments",
                column: "RefundTransactionId",
                unique: true,
                filter: "\"RefundTransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_CreationDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OpenReview",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RefundTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundIdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReviewResolvedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReviewResolvedBy",
                table: "Payments");
        }
    }
}
