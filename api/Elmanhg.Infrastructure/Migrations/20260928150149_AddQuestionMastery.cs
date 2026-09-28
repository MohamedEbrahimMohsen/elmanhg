using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionMastery : Migration
    {
        // PRD §7.3 threshold 0.8; migrations cannot read Mastery:CorrectThreshold.
        public const string BackfillSql = """
            WITH ranked AS (
                SELECT a."Id", a."StudentId", a."QuestionId", a."NormalisedScore", a."CreatedAt",
                       row_number() OVER (PARTITION BY a."StudentId", a."QuestionId" ORDER BY a."CreatedAt" DESC, a."Id" DESC) AS "Rank"
                FROM "Attempts" AS a
                INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
                WHERE s."IsTestMode" = false AND s."IsDeleted" = false AND a."IsDeleted" = false
            )
            INSERT INTO "QuestionMasteries" ("Id", "StudentId", "QuestionId", "IsMastered", "LatestAttemptId", "LatestNormalisedScore", "LatestAttemptedAt", "PreviousAttemptId", "PreviousNormalisedScore", "PreviousAttemptedAt", "CreatedBy", "CreationDate", "UpdatedBy", "UpdationDate", "IsDeleted", "DeletedAt")
            SELECT gen_random_uuid(), latest."StudentId", latest."QuestionId",
                   previous."Id" IS NOT NULL AND latest."NormalisedScore" >= 0.8 AND previous."NormalisedScore" >= 0.8,
                   latest."Id", latest."NormalisedScore", latest."CreatedAt",
                   previous."Id", previous."NormalisedScore", previous."CreatedAt",
                   latest."StudentId", latest."CreatedAt", latest."StudentId", latest."CreatedAt", false, NULL
            FROM ranked AS latest
            LEFT JOIN ranked AS previous ON previous."StudentId" = latest."StudentId" AND previous."QuestionId" = latest."QuestionId" AND previous."Rank" = 2
            WHERE latest."Rank" = 1
            ON CONFLICT ("StudentId", "QuestionId") DO NOTHING;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsMastered = table.Column<bool>(type: "boolean", nullable: false),
                    LatestAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LatestNormalisedScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    LatestAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousNormalisedScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    PreviousAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionMasteries_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionMasteries_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionMasteries_QuestionId",
                table: "QuestionMasteries",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionMasteries_StudentId_QuestionId",
                table: "QuestionMasteries",
                columns: new[] { "StudentId", "QuestionId" },
                unique: true);

            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionMasteries");
        }
    }
}
