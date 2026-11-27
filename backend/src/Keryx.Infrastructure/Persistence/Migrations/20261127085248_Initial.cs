using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Slug);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Slug);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IntervalHours = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    PublishThreshold = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    Topics = table.Column<string[]>(type: "text[]", nullable: false),
                    Exclusions = table.Column<string[]>(type: "text[]", nullable: false),
                    FocusAreas = table.Column<string[]>(type: "text[]", nullable: false),
                    IncludeOntarioWide = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeNational = table.Column<bool>(type: "boolean", nullable: false),
                    MergeDuplicates = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionMonths = table.Column<int>(type: "integer", nullable: false),
                    AlertEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    AlertFailureStreak = table.Column<int>(type: "integer", nullable: false),
                    AlertOnRunFailure = table.Column<bool>(type: "boolean", nullable: false),
                    DailyDigest = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.CheckConstraint("CK_Settings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RegionSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SeedKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sources_Regions_RegionSlug",
                        column: x => x.RegionSlug,
                        principalTable: "Regions",
                        principalColumn: "Slug",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Rank",
                table: "Regions",
                column: "Rank");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_RegionSlug",
                table: "Sources",
                column: "RegionSlug");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_SeedKey",
                table: "Sources",
                column: "SeedKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "Regions");
        }
    }
}
