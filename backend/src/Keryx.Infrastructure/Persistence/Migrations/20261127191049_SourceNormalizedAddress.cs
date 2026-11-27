using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourceNormalizedAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedAddress",
                table: "Sources",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            // Before this release only seed data created sources, and every seeded source is a
            // news query, whose normalized address is its query text as written.
            migrationBuilder.Sql("""UPDATE "Sources" SET "NormalizedAddress" = "Address" WHERE "NormalizedAddress" = '';""");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Kind_NormalizedAddress",
                table: "Sources",
                columns: new[] { "Kind", "NormalizedAddress" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sources_Kind_NormalizedAddress",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "NormalizedAddress",
                table: "Sources");
        }
    }
}
