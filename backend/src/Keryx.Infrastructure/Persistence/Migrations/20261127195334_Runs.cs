using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Keryx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Runs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Worker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Counts_Added = table.Column<int>(type: "integer", nullable: false),
                    Counts_Updated = table.Column<int>(type: "integer", nullable: false),
                    Counts_Unchanged = table.Column<int>(type: "integer", nullable: false),
                    Counts_Duplicates = table.Column<int>(type: "integer", nullable: false),
                    Counts_Held = table.Column<int>(type: "integer", nullable: false),
                    Counts_OffTopic = table.Column<int>(type: "integer", nullable: false),
                    Counts_Rejected = table.Column<int>(type: "integer", nullable: false),
                    Counts_SourcesOk = table.Column<int>(type: "integer", nullable: false),
                    Counts_SourcesFailed = table.Column<int>(type: "integer", nullable: false),
                    SourcesChecked = table.Column<int>(type: "integer", nullable: false),
                    SourcesTotal = table.Column<int>(type: "integer", nullable: false),
                    ExecutionStarted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunStages",
                columns: table => new
                {
                    Stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Counts = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunStages", x => new { x.RunId, x.Stage });
                    table.ForeignKey(
                        name: "FK_RunStages_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_PublicId",
                table: "Runs",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runs_Worker_StartedAt",
                table: "Runs",
                columns: new[] { "Worker", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunStages");

            migrationBuilder.DropTable(
                name: "Runs");
        }
    }
}
