using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EssayGradeTrainingRecords_EssayGradeId",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "MathStepGrades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewDecision",
                table: "MathStepGrades",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "MathStepGrades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "MathStepGrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewedNormalisedScore",
                table: "MathStepGrades",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewedScore",
                table: "MathStepGrades",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "EssayGradeTrainingRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewDecision",
                table: "EssayGradeTrainingRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "EssayGradeTrainingRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewedNormalisedScore",
                table: "EssayGradeTrainingRecords",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewedScore",
                table: "EssayGradeTrainingRecords",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trigger",
                table: "EssayGradeTrainingRecords",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "EssayGrades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewDecision",
                table: "EssayGrades",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "EssayGrades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "EssayGrades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewedNormalisedScore",
                table: "EssayGrades",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReviewedScore",
                table: "EssayGrades",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EssayGradeTrainingRecords_EssayGradeId_Trigger",
                table: "EssayGradeTrainingRecords",
                columns: new[] { "EssayGradeId", "Trigger" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EssayGradeTrainingRecords_EssayGradeId_Trigger",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "MathStepGrades");

            migrationBuilder.DropColumn(
                name: "ReviewDecision",
                table: "MathStepGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "MathStepGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "MathStepGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedNormalisedScore",
                table: "MathStepGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedScore",
                table: "MathStepGrades");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ReviewDecision",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ReviewedNormalisedScore",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ReviewedScore",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "EssayGradeTrainingRecords");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "ReviewDecision",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedNormalisedScore",
                table: "EssayGrades");

            migrationBuilder.DropColumn(
                name: "ReviewedScore",
                table: "EssayGrades");

            migrationBuilder.CreateIndex(
                name: "IX_EssayGradeTrainingRecords_EssayGradeId",
                table: "EssayGradeTrainingRecords",
                column: "EssayGradeId",
                unique: true);
        }
    }
}
