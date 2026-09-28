using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSittings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Deadline",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassMark",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnswerSavedAt",
                table: "SessionItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavedAnswer",
                table: "SessionItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OneOpenExam",
                table: "Sessions",
                column: "StudentId",
                unique: true,
                filter: "\"Kind\" <> 'Quiz' AND \"SubmittedAt\" IS NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OpenExamDeadline",
                table: "Sessions",
                column: "Deadline",
                filter: "\"SubmittedAt\" IS NULL AND \"Deadline\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_OneOpenExam",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_OpenExamDeadline",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PassMark",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AnswerSavedAt",
                table: "SessionItems");

            migrationBuilder.DropColumn(
                name: "SavedAnswer",
                table: "SessionItems");
        }
    }
}
