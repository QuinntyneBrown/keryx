using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourceFailureReasonAndHeldCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeldCount",
                table: "Sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "HeldCount",
                table: "Sources");
        }
    }
}
