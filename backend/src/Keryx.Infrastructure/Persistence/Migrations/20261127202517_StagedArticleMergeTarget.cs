using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StagedArticleMergeTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationKey",
                table: "StagedArticles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetUrl",
                table: "StagedArticles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StagedArticles_RunId_OrganizationKey",
                table: "StagedArticles",
                columns: new[] { "RunId", "OrganizationKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StagedArticles_RunId_OrganizationKey",
                table: "StagedArticles");

            migrationBuilder.DropColumn(
                name: "OrganizationKey",
                table: "StagedArticles");

            migrationBuilder.DropColumn(
                name: "TargetUrl",
                table: "StagedArticles");
        }
    }
}
