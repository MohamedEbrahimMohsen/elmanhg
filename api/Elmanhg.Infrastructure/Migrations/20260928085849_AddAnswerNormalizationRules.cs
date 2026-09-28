using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnswerNormalizationRules : Migration
    {
        public const string UpgradeGradingSpecsSql = """
            UPDATE "Questions" SET "GradingSpec" = ("GradingSpec" - 'unifyLetterVariants') || jsonb_build_object('normalization', jsonb_build_object('stripTashkeel', true, 'stripTatweel', true, 'unifyAlef', COALESCE(("GradingSpec" ->> 'unifyLetterVariants')::boolean, true), 'unifyTaaMarbuta', COALESCE(("GradingSpec" ->> 'unifyLetterVariants')::boolean, true), 'unifyAlefMaqsura', COALESCE(("GradingSpec" ->> 'unifyLetterVariants')::boolean, true), 'convertDigits', true, 'collapseWhitespace', true, 'foldCase', true)) WHERE "GradingSpec" ? 'unifyLetterVariants';
            """;

        public const string UpgradeRevisionSnapshotsSql = """
            UPDATE "QuestionRevisions" SET "Snapshot" = jsonb_set("Snapshot", ARRAY['gradingSpec'], (("Snapshot" -> 'gradingSpec') - 'unifyLetterVariants') || jsonb_build_object('normalization', jsonb_build_object('stripTashkeel', true, 'stripTatweel', true, 'unifyAlef', COALESCE(("Snapshot" -> 'gradingSpec' ->> 'unifyLetterVariants')::boolean, true), 'unifyTaaMarbuta', COALESCE(("Snapshot" -> 'gradingSpec' ->> 'unifyLetterVariants')::boolean, true), 'unifyAlefMaqsura', COALESCE(("Snapshot" -> 'gradingSpec' ->> 'unifyLetterVariants')::boolean, true), 'convertDigits', true, 'collapseWhitespace', true, 'foldCase', true))) WHERE ("Snapshot" -> 'gradingSpec') ? 'unifyLetterVariants';
            """;

        public const string DowngradeGradingSpecsSql = """
            UPDATE "Questions" SET "GradingSpec" = ("GradingSpec" - 'normalization') || jsonb_build_object('unifyLetterVariants', COALESCE(("GradingSpec" -> 'normalization' ->> 'unifyAlef')::boolean, true)) WHERE "GradingSpec" ? 'normalization';
            """;

        public const string DowngradeRevisionSnapshotsSql = """
            UPDATE "QuestionRevisions" SET "Snapshot" = jsonb_set("Snapshot", ARRAY['gradingSpec'], (("Snapshot" -> 'gradingSpec') - 'normalization') || jsonb_build_object('unifyLetterVariants', COALESCE(("Snapshot" -> 'gradingSpec' -> 'normalization' ->> 'unifyAlef')::boolean, true))) WHERE ("Snapshot" -> 'gradingSpec') ? 'normalization';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpgradeGradingSpecsSql);
            migrationBuilder.Sql(UpgradeRevisionSnapshotsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DowngradeGradingSpecsSql);
            migrationBuilder.Sql(DowngradeRevisionSnapshotsSql);
        }
    }
}
