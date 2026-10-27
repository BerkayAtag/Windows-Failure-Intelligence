using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "RemediationExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CampaignPilot",
                table: "RemediationExecutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RepairCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PlaybookId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaybookCode = table.Column<string>(type: "text", nullable: false),
                    TargetsJson = table.Column<string>(type: "text", nullable: false),
                    PilotLimit = table.Column<int>(type: "integer", nullable: false),
                    MaxParallel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ExpandedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairCampaigns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationExecutions_CampaignId_Status",
                table: "RemediationExecutions",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairCampaigns_Domain_Status",
                table: "RepairCampaigns",
                columns: new[] { "Domain", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_RemediationExecutions_CampaignId_Status",
                table: "RemediationExecutions");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "RemediationExecutions");

            migrationBuilder.DropColumn(
                name: "CampaignPilot",
                table: "RemediationExecutions");
        }
    }
}
