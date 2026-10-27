using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerifiedExperienceMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LearningExcluded",
                table: "RemediationExecutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LearningContextJson",
                table: "AiAnalysisJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationExecutions_Domain_Status_CompletedAt",
                table: "RemediationExecutions",
                columns: new[] { "Domain", "Status", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RemediationExecutions_Domain_Status_CompletedAt",
                table: "RemediationExecutions");

            migrationBuilder.DropColumn(
                name: "LearningExcluded",
                table: "RemediationExecutions");

            migrationBuilder.DropColumn(
                name: "LearningContextJson",
                table: "AiAnalysisJobs");
        }
    }
}
