using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartialIndexForAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_company_amendments_CompanyId_SequenceNumber",
                table: "company_amendments");

            migrationBuilder.CreateIndex(
                name: "IX_company_amendments_CompanyId_SequenceNumber",
                table: "company_amendments",
                columns: new[] { "CompanyId", "SequenceNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_company_amendments_CompanyId_SequenceNumber",
                table: "company_amendments");

            migrationBuilder.CreateIndex(
                name: "IX_company_amendments_CompanyId_SequenceNumber",
                table: "company_amendments",
                columns: new[] { "CompanyId", "SequenceNumber" },
                unique: true);
        }
    }
}
