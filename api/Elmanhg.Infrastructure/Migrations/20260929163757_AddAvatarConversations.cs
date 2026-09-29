using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvatarConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryPoint = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AvatarConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvatarConversations_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvatarConversations_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvatarConversations_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvatarConversations_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvatarConversations_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvatarConversations_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AvatarMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CostUsd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    StopReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HistoryMessageCount = table.Column<int>(type: "integer", nullable: true),
                    Context = table.Column<string>(type: "jsonb", nullable: true),
                    Citations = table.Column<string>(type: "jsonb", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvatarMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvatarMessages_AvatarConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AvatarConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_LastMessageAt",
                table: "AvatarConversations",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_LessonId",
                table: "AvatarConversations",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_QuestionId",
                table: "AvatarConversations",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_SessionId",
                table: "AvatarConversations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_StudentId_LastMessageAt",
                table: "AvatarConversations",
                columns: new[] { "StudentId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_SubjectId",
                table: "AvatarConversations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarConversations_UnitId",
                table: "AvatarConversations",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarMessages_ConversationId_Position",
                table: "AvatarMessages",
                columns: new[] { "ConversationId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvatarMessages_CreatedAt",
                table: "AvatarMessages",
                column: "CreatedAt");

            migrationBuilder.Sql("""
                CREATE TRIGGER avatar_messages_append_only BEFORE UPDATE OR DELETE ON "AvatarMessages" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER avatar_messages_no_truncate BEFORE TRUNCATE ON "AvatarMessages" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER avatar_messages_no_truncate ON "AvatarMessages";
                DROP TRIGGER avatar_messages_append_only ON "AvatarMessages";
                """);

            migrationBuilder.DropTable(
                name: "AvatarMessages");

            migrationBuilder.DropTable(
                name: "AvatarConversations");
        }
    }
}
