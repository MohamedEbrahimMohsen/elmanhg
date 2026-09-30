using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttemptTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionVersion = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Answer = table.Column<string>(type: "jsonb", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    NormalisedScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    GradedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Grade = table.Column<string>(type: "jsonb", nullable: true),
                    TimeTakenMilliseconds = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvatarTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentMessagePosition = table.Column<int>(type: "integer", nullable: false),
                    EntryPoint = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentText = table.Column<string>(type: "text", nullable: false),
                    AssistantText = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Context = table.Column<string>(type: "jsonb", nullable: false),
                    AskedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvatarTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherThreadTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionVersion = table.Column<int>(type: "integer", nullable: true),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Context = table.Column<string>(type: "jsonb", nullable: false),
                    Messages = table.Column<string>(type: "jsonb", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherThreadTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptTrainingRecords_AttemptId",
                table: "AttemptTrainingRecords",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptTrainingRecords_OccurredAt",
                table: "AttemptTrainingRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptTrainingRecords_StudentHash",
                table: "AttemptTrainingRecords",
                column: "StudentHash");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarTrainingRecords_ConversationId_StudentMessagePosition",
                table: "AvatarTrainingRecords",
                columns: new[] { "ConversationId", "StudentMessagePosition" });

            migrationBuilder.CreateIndex(
                name: "IX_AvatarTrainingRecords_OccurredAt",
                table: "AvatarTrainingRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarTrainingRecords_StudentHash",
                table: "AvatarTrainingRecords",
                column: "StudentHash");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarTrainingRecords_StudentMessageId",
                table: "AvatarTrainingRecords",
                column: "StudentMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadTrainingRecords_OccurredAt",
                table: "TeacherThreadTrainingRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadTrainingRecords_StudentHash",
                table: "TeacherThreadTrainingRecords",
                column: "StudentHash");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherThreadTrainingRecords_ThreadId_Trigger",
                table: "TeacherThreadTrainingRecords",
                columns: new[] { "ThreadId", "Trigger" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER attempt_training_records_append_only BEFORE UPDATE OR DELETE ON "AttemptTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER attempt_training_records_no_truncate BEFORE TRUNCATE ON "AttemptTrainingRecords" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER avatar_training_records_append_only BEFORE UPDATE OR DELETE ON "AvatarTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER avatar_training_records_no_truncate BEFORE TRUNCATE ON "AvatarTrainingRecords" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER teacher_thread_training_records_append_only BEFORE UPDATE OR DELETE ON "TeacherThreadTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER teacher_thread_training_records_no_truncate BEFORE TRUNCATE ON "TeacherThreadTrainingRecords" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER teacher_thread_training_records_no_truncate ON "TeacherThreadTrainingRecords";
                DROP TRIGGER teacher_thread_training_records_append_only ON "TeacherThreadTrainingRecords";
                DROP TRIGGER avatar_training_records_no_truncate ON "AvatarTrainingRecords";
                DROP TRIGGER avatar_training_records_append_only ON "AvatarTrainingRecords";
                DROP TRIGGER attempt_training_records_no_truncate ON "AttemptTrainingRecords";
                DROP TRIGGER attempt_training_records_append_only ON "AttemptTrainingRecords";
                """);

            migrationBuilder.DropTable(
                name: "AttemptTrainingRecords");

            migrationBuilder.DropTable(
                name: "AvatarTrainingRecords");

            migrationBuilder.DropTable(
                name: "TeacherThreadTrainingRecords");
        }
    }
}
