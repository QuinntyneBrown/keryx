using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Organizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrganizationId",
                table: "Articles",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RegionSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_Regions_RegionSlug",
                        column: x => x.RegionSlug,
                        principalTable: "Regions",
                        principalColumn: "Slug",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationProvenance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProvenance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationProvenance_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationProvenance_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_OrganizationId",
                table: "Articles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProvenance_OrganizationId",
                table: "OrganizationProvenance",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProvenance_SourceId",
                table: "OrganizationProvenance",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_NormalizedName",
                table: "Organizations",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_PublicId",
                table: "Organizations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_RegionSlug",
                table: "Organizations",
                column: "RegionSlug");

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Organizations_OrganizationId",
                table: "Articles",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Articles_Organizations_OrganizationId",
                table: "Articles");

            migrationBuilder.DropTable(
                name: "OrganizationProvenance");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Articles_OrganizationId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Articles");
        }
    }
}
