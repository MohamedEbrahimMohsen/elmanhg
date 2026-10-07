using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elmanhg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpVersionAndRecipientIndex : Migration
    {
        public const string DeduplicateRecipientsSql = """
            DELETE FROM "Otps" WHERE "Id" IN (SELECT "Id" FROM (SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "PhoneNumber" ORDER BY "CreatedAt" DESC, "Id" DESC) AS "Rank" FROM "Otps" WHERE "IsDeleted" = false) AS "Ranked" WHERE "Rank" > 1);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Otps",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.Sql(DeduplicateRecipientsSql);

            migrationBuilder.CreateIndex(
                name: "IX_Otps_PhoneNumber",
                table: "Otps",
                column: "PhoneNumber",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Otps_PhoneNumber",
                table: "Otps");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Otps");
        }
    }
}
