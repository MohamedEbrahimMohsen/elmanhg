using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogDiffAndAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Diff",
                table: "AuditLogs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceType_Timestamp",
                table: "AuditLogs",
                columns: new[] { "ResourceType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.Sql("""
                CREATE FUNCTION audit_logs_reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'AuditLogs is append-only'; END; $$;
                CREATE TRIGGER audit_logs_append_only BEFORE UPDATE OR DELETE ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION audit_logs_reject_mutation();
                CREATE TRIGGER audit_logs_no_truncate BEFORE TRUNCATE ON "AuditLogs" FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER audit_logs_no_truncate ON "AuditLogs";
                DROP TRIGGER audit_logs_append_only ON "AuditLogs";
                DROP FUNCTION audit_logs_reject_mutation();
                """);

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ResourceType_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Diff",
                table: "AuditLogs");
        }
    }
}
