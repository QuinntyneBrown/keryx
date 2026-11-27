using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Articles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RegionSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tags = table.Column<string>(type: "character varying(1300)", maxLength: 1300, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_Regions_RegionSlug",
                        column: x => x.RegionSlug,
                        principalTable: "Regions",
                        principalColumn: "Slug",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Articles_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StagedArticles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RegionSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tags = table.Column<string>(type: "character varying(1300)", maxLength: 1300, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagedArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StagedArticles_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleProvenance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleProvenance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleProvenance_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleProvenance_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleProvenance_ArticleId",
                table: "ArticleProvenance",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleProvenance_SourceId",
                table: "ArticleProvenance",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_PublicId",
                table: "Articles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_RegionSlug",
                table: "Articles",
                column: "RegionSlug");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_SourceId",
                table: "Articles",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Status_CollectedAt",
                table: "Articles",
                columns: new[] { "Status", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Url",
                table: "Articles",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StagedArticles_RunId_Url",
                table: "StagedArticles",
                columns: new[] { "RunId", "Url" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleProvenance");

            migrationBuilder.DropTable(
                name: "StagedArticles");

            migrationBuilder.DropTable(
                name: "Articles");
        }
    }
}
