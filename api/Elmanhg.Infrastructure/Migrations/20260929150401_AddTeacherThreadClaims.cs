using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherThreadClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimedAt",
                table: "TeacherThreads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "TeacherThreads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StudentReadAt",
                table: "TeacherMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreads_TeacherId",
                table: "TeacherThreads",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherThreads_AspNetUsers_TeacherId",
                table: "TeacherThreads",
                column: "TeacherId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherThreads_AspNetUsers_TeacherId",
                table: "TeacherThreads");

            migrationBuilder.DropIndex(
                name: "IX_TeacherThreads_TeacherId",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "TeacherThreads");

            migrationBuilder.DropColumn(
                name: "StudentReadAt",
                table: "TeacherMessages");
        }
    }
}
