using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EssayGradeTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EssayGradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionVersion = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Answer = table.Column<string>(type: "jsonb", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    NormalisedScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Criteria = table.Column<string>(type: "jsonb", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EssayGradeTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FileKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    RowCount = table.Column<long>(type: "bigint", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_TrainingExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingExports_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EssayGradeTrainingRecords_EssayGradeId",
                table: "EssayGradeTrainingRecords",
                column: "EssayGradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EssayGradeTrainingRecords_OccurredAt",
                table: "EssayGradeTrainingRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_EssayGradeTrainingRecords_StudentHash",
                table: "EssayGradeTrainingRecords",
                column: "StudentHash");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingExports_ExpiresAt",
                table: "TrainingExports",
                column: "ExpiresAt",
                filter: "\"Status\" = 'Completed'");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingExports_NextAttemptAt",
                table: "TrainingExports",
                column: "NextAttemptAt",
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingExports_RequestedAt",
                table: "TrainingExports",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingExports_SubjectId",
                table: "TrainingExports",
                column: "SubjectId");

            migrationBuilder.Sql("""
                CREATE TRIGGER essay_grade_training_records_append_only BEFORE UPDATE OR DELETE ON "EssayGradeTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();
                CREATE TRIGGER essay_grade_training_records_no_truncate BEFORE TRUNCATE ON "EssayGradeTrainingRecords" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER essay_grade_training_records_no_truncate ON "EssayGradeTrainingRecords";
                DROP TRIGGER essay_grade_training_records_append_only ON "EssayGradeTrainingRecords";
                """);

            migrationBuilder.DropTable(
                name: "EssayGradeTrainingRecords");

            migrationBuilder.DropTable(
                name: "TrainingExports");
        }
    }
}
