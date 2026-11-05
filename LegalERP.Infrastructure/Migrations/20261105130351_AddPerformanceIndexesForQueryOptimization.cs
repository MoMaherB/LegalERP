using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesForQueryOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_companies_Category",
                table: "companies",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_companies_RegistrationNumber",
                table: "companies",
                column: "RegistrationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_clients_FullName_IsDeleted",
                table: "clients",
                columns: new[] { "FullName", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_NationalIdNumber",
                table: "clients",
                column: "NationalIdNumber");

            migrationBuilder.CreateIndex(
                name: "IX_cases_FilingDate",
                table: "cases",
                column: "FilingDate");

            migrationBuilder.CreateIndex(
                name: "IX_cases_Status_IsDeleted",
                table: "cases",
                columns: new[] { "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_case_hearings_HearingDate_IsDeleted",
                table: "case_hearings",
                columns: new[] { "HearingDate", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_companies_Category",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_RegistrationNumber",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_clients_FullName_IsDeleted",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "IX_clients_NationalIdNumber",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "IX_cases_FilingDate",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "IX_cases_Status_IsDeleted",
                table: "cases");

            migrationBuilder.DropIndex(
                name: "IX_case_hearings_HearingDate_IsDeleted",
                table: "case_hearings");
        }
    }
}
