using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherVoiceReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioDurationSeconds",
                table: "TeacherMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "TeacherMessages",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TranscriptFinal",
                table: "TeacherMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TeacherVoiceDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    AudioUrl = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    AudioDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Transcript = table.Column<string>(type: "text", nullable: true),
                    TranscriptionModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TranscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherVoiceDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherVoiceDrafts_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherVoiceDrafts_TeacherThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "TeacherThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherMessages_AudioUrl",
                table: "TeacherMessages",
                column: "AudioUrl",
                filter: "\"AudioUrl\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherVoiceDrafts_NextAttemptAt",
                table: "TeacherVoiceDrafts",
                column: "NextAttemptAt",
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherVoiceDrafts_TeacherId",
                table: "TeacherVoiceDrafts",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherVoiceDrafts_ThreadId_TeacherId",
                table: "TeacherVoiceDrafts",
                columns: new[] { "ThreadId", "TeacherId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherVoiceDrafts");

            migrationBuilder.DropIndex(
                name: "IX_TeacherMessages_AudioUrl",
                table: "TeacherMessages");

            migrationBuilder.DropColumn(
                name: "AudioDurationSeconds",
                table: "TeacherMessages");

            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "TeacherMessages");

            migrationBuilder.DropColumn(
                name: "TranscriptFinal",
                table: "TeacherMessages");
        }
    }
}
