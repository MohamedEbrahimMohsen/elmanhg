using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEssayGradeTimeTaken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAt",
                table: "EssayGrades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeTakenMilliseconds",
                table: "EssayGrades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EssayGrades_GradedAt",
                table: "EssayGrades",
                column: "GradedAt",
                filter: "\"Status\" = 'Graded' AND \"AppliedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EssayGrades_GradedAt",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "TimeTakenMilliseconds",
                table: "EssayGrades");
        }
    }
}
