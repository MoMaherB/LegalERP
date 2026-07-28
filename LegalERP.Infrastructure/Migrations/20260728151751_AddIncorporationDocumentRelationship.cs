using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncorporationDocumentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_companies_IncorporationDocumentId",
                table: "companies",
                column: "IncorporationDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_companies_documents_IncorporationDocumentId",
                table: "companies",
                column: "IncorporationDocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_companies_documents_IncorporationDocumentId",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_IncorporationDocumentId",
                table: "companies");
        }
    }
}
