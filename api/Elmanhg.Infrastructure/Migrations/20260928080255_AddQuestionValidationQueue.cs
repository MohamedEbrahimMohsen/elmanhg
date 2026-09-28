using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionValidationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "QuestionDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DifficultyChangedFrom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionDecisions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessions_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSessionOpenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionVersion = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessionOpenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessionOpenings_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewSessionOpenings_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionDecisions_QuestionId_DecidedAt",
                table: "QuestionDecisions",
                columns: new[] { "QuestionId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessionOpenings_QuestionId",
                table: "ReviewSessionOpenings",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessionOpenings_ReviewSessionId_QuestionId",
                table: "ReviewSessionOpenings",
                columns: new[] { "ReviewSessionId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessions_TeacherId",
                table: "ReviewSessions",
                column: "TeacherId");

            migrationBuilder.Sql("""
                UPDATE "Questions" AS q SET "SubmittedAt" = COALESCE((SELECT MAX(r."EditedAt") FROM "QuestionRevisions" AS r WHERE r."QuestionId" = q."Id"), q."CreationDate");
                """);

            migrationBuilder.Sql("""
                INSERT INTO "QuestionDecisions" ("Id", "QuestionId", "Version", "Outcome", "Reason", "Difficulty", "DifficultyChangedFrom", "DecidedBy", "DecidedAt", "IsDeleted", "DeletedAt")
                SELECT gen_random_uuid(), q."Id", q."Version", q."ValidationStatus", q."RejectionReason", q."Difficulty", NULL, q."ValidatedBy", q."ValidatedAt", false, NULL
                FROM "Questions" AS q
                WHERE q."ValidationStatus" IN ('Approved', 'Rejected') AND q."ValidatedBy" IS NOT NULL AND q."ValidatedAt" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionDecisions");

            migrationBuilder.DropTable(
                name: "ReviewSessionOpenings");

            migrationBuilder.DropTable(
                name: "ReviewSessions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Questions");
        }
    }
}
