using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WFI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemediationCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Playbooks",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "Description", "IsApproved", "Mode", "PrechecksScript", "RequiredRole", "RiskLevel", "RollbackScript", "ScriptContent", "Signature", "Title", "Version" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Windows System", "PB-DNS-FLUSH", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "DNS istemci önbelleğini temizle", true, "AutoRemediate", "", "Analyst", "Low", "", "", "", "DNS istemci önbelleğini temizle", "3.0.0" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Windows System", "PB-WU-START", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Windows Update servislerini başlat", true, "AutoRemediate", "", "Analyst", "Low", "", "", "", "Windows Update servislerini başlat", "3.0.0" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Windows System", "PB-SCCM-RESTART", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SCCM istemci servisini yeniden başlat", true, "Recommend", "", "Analyst", "Medium", "", "", "", "SCCM istemci servisini yeniden başlat", "3.0.0" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Windows System", "PB-DISM-REPAIR", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Windows bileşen deposunu onar", true, "Recommend", "", "Admin", "High", "", "", "", "Windows bileşen deposunu onar", "3.0.0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Playbooks",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));
        }
    }
}
