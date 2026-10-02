using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WindowStartedAt",
                table: "TeacherThreadSlaEvents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstReminderDueAt",
                table: "TeacherThreads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SecondReminderDueAt",
                table: "TeacherThreads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SlaScheduleFingerprint",
                table: "TeacherThreads",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaWindowStartedAt",
                table: "TeacherThreads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "ExamPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_ExamPeriods", x => x.Id);
                    table.CheckConstraint("CK_ExamPeriods_DateRange", "\"EndDate\" >= \"StartDate\"");
                });

            migrationBuilder.Sql("""
                UPDATE "TeacherThreads" AS t SET "SlaWindowStartedAt" = COALESCE((SELECT MAX(m."CreatedAt") FROM "TeacherMessages" AS m WHERE m."ThreadId" = t."Id" AND m."SenderId" = t."StudentId"), t."SubmittedAt"), "FirstReminderDueAt" = t."SlaDueAt", "SecondReminderDueAt" = t."SlaDueAt", "SlaScheduleFingerprint" = '';
                UPDATE "TeacherThreadSlaEvents" AS e SET "WindowStartedAt" = COALESCE((SELECT MAX(m."CreatedAt") FROM "TeacherMessages" AS m INNER JOIN "TeacherThreads" AS t ON t."Id" = m."ThreadId" WHERE m."ThreadId" = e."ThreadId" AND m."SenderId" = t."StudentId" AND m."CreatedAt" <= e."OccurredAt"), e."SlaDueAt");
                """);

            migrationBuilder.DropIndex(
                name: "IX_TeacherThreadSlaEvents_ThreadId_Kind_SlaDueAt",
                table: "TeacherThreadSlaEvents");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadSlaEvents_ThreadId_Kind_WindowStartedAt",
                table: "TeacherThreadSlaEvents",
                columns: new[] { "ThreadId", "Kind", "WindowStartedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamPeriods_StartDate_EndDate",
                table: "ExamPeriods",
                columns: new[] { "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamPeriods");

            migrationBuilder.DropIndex(
                name: "IX_TeacherThreadSlaEvents_ThreadId_Kind_WindowStartedAt",
                table: "TeacherThreadSlaEvents");

            migrationBuilder.DropColumn(
                name: "WindowStartedAt",
                table: "TeacherThreadSlaEvents");

            migrationBuilder.DropColumn(
                name: "FirstReminderDueAt",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "SecondReminderDueAt",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "SlaScheduleFingerprint",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "SlaWindowStartedAt",
                table: "TeacherThreads");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadSlaEvents_ThreadId_Kind_SlaDueAt",
                table: "TeacherThreadSlaEvents",
                columns: new[] { "ThreadId", "Kind", "SlaDueAt" },
                unique: true);
        }
    }
}
