using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "QuestionDecisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"QuestionDecisions\" AS d SET \"SubmittedAt\" = r.\"EditedAt\" FROM \"QuestionRevisions\" AS r WHERE r.\"QuestionId\" = d.\"QuestionId\" AND r.\"Version\" = d.\"Version\";");
            migrationBuilder.Sql("UPDATE \"QuestionDecisions\" SET \"SubmittedAt\" = \"DecidedAt\" WHERE \"SubmittedAt\" IS NULL;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "QuestionDecisions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "UserActivityDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivityDays_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadSlaEvents_Kind_OccurredAt",
                table: "TeacherThreadSlaEvents",
                columns: new[] { "Kind", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMessages_CreatedAt",
                table: "TeacherMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionDecisions_DecidedAt",
                table: "QuestionDecisions",
                column: "DecidedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompletedAt",
                table: "Payments",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_CreatedAt",
                table: "Attempts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Role_CreationDate",
                table: "AspNetUsers",
                columns: new[] { "Role", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityDays_Day",
                table: "UserActivityDays",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityDays_UserId_Day",
                table: "UserActivityDays",
                columns: new[] { "UserId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActivityDays");

            migrationBuilder.DropIndex(
                name: "IX_TeacherThreadSlaEvents_Kind_OccurredAt",
                table: "TeacherThreadSlaEvents");

            migrationBuilder.DropIndex(
                name: "IX_TeacherMessages_CreatedAt",
                table: "TeacherMessages");

            migrationBuilder.DropIndex(
                name: "IX_QuestionDecisions_DecidedAt",
                table: "QuestionDecisions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CompletedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Attempts_CreatedAt",
                table: "Attempts");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Role_CreationDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "QuestionDecisions");
        }
    }
}
