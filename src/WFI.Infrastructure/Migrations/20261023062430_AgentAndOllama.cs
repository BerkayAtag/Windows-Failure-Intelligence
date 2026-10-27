using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WFI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgentAndOllama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiagnosticJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: false),
                    ModelUsed = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PromptContext = table.Column<string>(type: "text", nullable: false),
                    Conclusion = table.Column<string>(type: "text", nullable: false),
                    HypothesesJson = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    MissingEvidenceJson = table.Column<string>(type: "text", nullable: false),
                    RecommendedPlaybooksJson = table.Column<string>(type: "text", nullable: false),
                    RawResponse = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    ActiveModel = table.Column<string>(type: "text", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationPolicies",
                columns: table => new
                {
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationPolicies", x => x.Domain);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    OsVersion = table.Column<string>(type: "text", nullable: false),
                    AgentVersion = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CertificateThumbprint = table.Column<string>(type: "text", nullable: true),
                    EnrollmentToken = table.Column<string>(type: "text", nullable: true),
                    EnrollmentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgentCredentialHash = table.Column<string>(type: "text", nullable: true),
                    AutomationMode = table.Column<string>(type: "text", nullable: false),
                    SystemInfoJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: false),
                    ScanType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EvidenceJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Heartbeats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CpuUsagePercent = table.Column<double>(type: "double precision", nullable: false),
                    MemoryUsagePercent = table.Column<double>(type: "double precision", nullable: false),
                    DiskFreeSpaceGb = table.Column<double>(type: "double precision", nullable: false),
                    HealthStatus = table.Column<string>(type: "text", nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Heartbeats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Playbooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    RequiredRole = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    PrechecksScript = table.Column<string>(type: "text", nullable: false),
                    ScriptContent = table.Column<string>(type: "text", nullable: false),
                    RollbackScript = table.Column<string>(type: "text", nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playbooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    AffectedHostname = table.Column<string>(type: "text", nullable: false),
                    PrimaryCategory = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    DiagnosticJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiAnalysisId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AiAnalysisJobId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_AiAnalysisJobs_AiAnalysisJobId",
                        column: x => x.AiAnalysisJobId,
                        principalTable: "AiAnalysisJobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Incidents_DiagnosticJobs_DiagnosticJobId",
                        column: x => x.DiagnosticJobId,
                        principalTable: "DiagnosticJobs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RemediationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiagnosticJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Automatic = table.Column<bool>(type: "boolean", nullable: false),
                    PlaybookId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaybookCode = table.Column<string>(type: "text", nullable: false),
                    PlaybookTitle = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Hostname = table.Column<string>(type: "text", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PrecheckResult = table.Column<string>(type: "text", nullable: false),
                    ExecutionOutput = table.Column<string>(type: "text", nullable: false),
                    ValidationResult = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationExecutions_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisJobs_DiagnosticJobId",
                table: "AiAnalysisJobs",
                column: "DiagnosticJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisJobs_Domain_CreatedAt",
                table: "AiAnalysisJobs",
                columns: new[] { "Domain", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_AgentCredentialHash",
                table: "Devices",
                column: "AgentCredentialHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CertificateThumbprint",
                table: "Devices",
                column: "CertificateThumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Domain_Hostname",
                table: "Devices",
                columns: new[] { "Domain", "Hostname" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_EnrollmentToken",
                table: "Devices",
                column: "EnrollmentToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticJobs_DeviceId",
                table: "DiagnosticJobs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticJobs_DeviceId_Status_LeaseUntil",
                table: "DiagnosticJobs",
                columns: new[] { "DeviceId", "Status", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticJobs_Domain_Status_CreatedAt",
                table: "DiagnosticJobs",
                columns: new[] { "Domain", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Heartbeats_DeviceId_Timestamp",
                table: "Heartbeats",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Heartbeats_Hostname",
                table: "Heartbeats",
                column: "Hostname");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_AiAnalysisJobId",
                table: "Incidents",
                column: "AiAnalysisJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CreatedAt",
                table: "Incidents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DiagnosticJobId",
                table: "Incidents",
                column: "DiagnosticJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Domain_Status_CreatedAt",
                table: "Incidents",
                columns: new[] { "Domain", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Playbooks_Code",
                table: "Playbooks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemediationExecutions_DeviceId_Status",
                table: "RemediationExecutions",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationExecutions_Hostname_RequestedAt",
                table: "RemediationExecutions",
                columns: new[] { "Hostname", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationExecutions_PlaybookId",
                table: "RemediationExecutions",
                column: "PlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationExecutions_Status",
                table: "RemediationExecutions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiConfigs");

            migrationBuilder.DropTable(
                name: "AutomationPolicies");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Heartbeats");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "RemediationExecutions");

            migrationBuilder.DropTable(
                name: "AiAnalysisJobs");

            migrationBuilder.DropTable(
                name: "DiagnosticJobs");

            migrationBuilder.DropTable(
                name: "Playbooks");
        }
    }
}
