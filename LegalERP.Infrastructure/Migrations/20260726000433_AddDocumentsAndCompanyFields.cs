using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentsAndCompanyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "companies",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EstablishmentDate",
                table: "companies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_partners_NationalIdDocumentId",
                table: "company_partners",
                column: "NationalIdDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_company_amendments_DocumentId",
                table: "company_amendments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_OwnerType_OwnerId",
                table: "documents",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_company_amendments_documents_DocumentId",
                table: "company_amendments",
                column: "DocumentId",
                principalTable: "documents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_company_partners_documents_NationalIdDocumentId",
                table: "company_partners",
                column: "NationalIdDocumentId",
                principalTable: "documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_company_amendments_documents_DocumentId",
                table: "company_amendments");

            migrationBuilder.DropForeignKey(
                name: "FK_company_partners_documents_NationalIdDocumentId",
                table: "company_partners");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropIndex(
                name: "IX_company_partners_NationalIdDocumentId",
                table: "company_partners");

            migrationBuilder.DropIndex(
                name: "IX_company_amendments_DocumentId",
                table: "company_amendments");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "EstablishmentDate",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "companies");
        }
    }
}
