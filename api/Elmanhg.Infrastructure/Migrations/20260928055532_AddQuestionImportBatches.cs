using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuestionImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionImportBatches_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_ImportBatchId",
                table: "Questions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionImportBatches_LessonId",
                table: "QuestionImportBatches",
                column: "LessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_QuestionImportBatches_ImportBatchId",
                table: "Questions",
                column: "ImportBatchId",
                principalTable: "QuestionImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_QuestionImportBatches_ImportBatchId",
                table: "Questions");

            migrationBuilder.DropTable(
                name: "QuestionImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Questions_ImportBatchId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Questions");
        }
    }
}
