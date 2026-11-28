using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SettingsVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Settings");
        }
    }
}
