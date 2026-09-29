using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherThreadSlaAndRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "TeacherThreads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "TeacherThreads",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeacherThreadSlaEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SlaDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherThreadSlaEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherThreadSlaEvents_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherThreadSlaEvents_TeacherThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "TeacherThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreads_Status_SlaDueAt",
                table: "TeacherThreads",
                columns: new[] { "Status", "SlaDueAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeacherThreads_Rating",
                table: "TeacherThreads",
                sql: "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadSlaEvents_TeacherId",
                table: "TeacherThreadSlaEvents",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadSlaEvents_ThreadId_Kind_SlaDueAt",
                table: "TeacherThreadSlaEvents",
                columns: new[] { "ThreadId", "Kind", "SlaDueAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherThreadSlaEvents");

            migrationBuilder.DropIndex(
                name: "IX_TeacherThreads_Status_SlaDueAt",
                table: "TeacherThreads");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeacherThreads_Rating",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "TeacherThreads");
        }
    }
}
