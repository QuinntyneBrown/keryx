using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunLeasesAndIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Worker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Severity = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: true),
                    Stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ConsecutiveRuns = table.Column<int>(type: "integer", nullable: false),
                    Since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunIssues_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunLeases",
                columns: table => new
                {
                    Worker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActiveRunId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LastScheduledSlotAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunLeases", x => x.Worker);
                });

            migrationBuilder.CreateTable(
                name: "IssueOccurrences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssueId = table.Column<long>(type: "bigint", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueOccurrences_RunIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "RunIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueOccurrences_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueOccurrences_IssueId",
                table: "IssueOccurrences",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueOccurrences_RunId_IssueId",
                table: "IssueOccurrences",
                columns: new[] { "RunId", "IssueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunIssues_PublicId",
                table: "RunIssues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunIssues_SourceId",
                table: "RunIssues",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RunIssues_State_Severity_Since",
                table: "RunIssues",
                columns: new[] { "State", "Severity", "Since" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueOccurrences");

            migrationBuilder.DropTable(
                name: "RunLeases");

            migrationBuilder.DropTable(
                name: "RunIssues");
        }
    }
}
