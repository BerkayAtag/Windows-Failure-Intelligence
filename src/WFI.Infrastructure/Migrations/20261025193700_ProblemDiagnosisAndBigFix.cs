using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProblemDiagnosisAndBigFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProblemDescription",
                table: "DiagnosticJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "Version",
                value: "4.0.0");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "Version",
                value: "4.0.0");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "Version",
                value: "4.0.0");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "Version",
                value: "4.0.0");

            migrationBuilder.InsertData(
                table: "Playbooks",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "Description", "IsApproved", "Mode", "PrechecksScript", "RequiredRole", "RiskLevel", "RollbackScript", "ScriptContent", "Signature", "Title", "Version" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000005"), "Windows System", "PB-BIGFIX-REPAIR", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "BigFix BESClient servisini onar", true, "AutoRemediate", "", "Admin", "High", "", "", "", "BigFix BESClient servisini onar", "4.0.0" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DropColumn(
                name: "ProblemDescription",
                table: "DiagnosticJobs");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "Version",
                value: "3.0.0");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "Version",
                value: "3.0.0");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "Version",
                value: "3.0.0");

            migrationBuilder.UpdateData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "Version",
                value: "3.0.0");
        }
    }
}
