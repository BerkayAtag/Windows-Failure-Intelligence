using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BigFixCentralVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BigFixMappings",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    ComputerId = table.Column<long>(type: "bigint", nullable: false),
                    ConnectorFingerprint = table.Column<string>(type: "text", nullable: false),
                    VerifiedDnsName = table.Column<string>(type: "text", nullable: false),
                    VerifiedBy = table.Column<string>(type: "text", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BigFixMappings", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "BigFixVerifications",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    ComputerId = table.Column<long>(type: "bigint", nullable: false),
                    ConnectorFingerprint = table.Column<string>(type: "text", nullable: false),
                    ExpectedDnsName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    BaselineReady = table.Column<bool>(type: "boolean", nullable: false),
                    BeforeReportAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestReportAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextCheckAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchToken = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BigFixVerifications", x => x.ExecutionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BigFixMappings_Domain_ConnectorFingerprint_ComputerId",
                table: "BigFixMappings",
                columns: new[] { "Domain", "ConnectorFingerprint", "ComputerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BigFixVerifications_Status_NextCheckAt",
                table: "BigFixVerifications",
                columns: new[] { "Status", "NextCheckAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BigFixMappings");

            migrationBuilder.DropTable(
                name: "BigFixVerifications");
        }
    }
}
