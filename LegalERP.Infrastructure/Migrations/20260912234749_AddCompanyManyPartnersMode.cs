using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyManyPartnersMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasManyPartners",
                table: "companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnersAttorneysDocumentId",
                table: "companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnersIdsDocumentId",
                table: "companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnersText",
                table: "companies",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_companies_PartnersAttorneysDocumentId",
                table: "companies",
                column: "PartnersAttorneysDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_companies_PartnersIdsDocumentId",
                table: "companies",
                column: "PartnersIdsDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_companies_documents_PartnersAttorneysDocumentId",
                table: "companies",
                column: "PartnersAttorneysDocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_companies_documents_PartnersIdsDocumentId",
                table: "companies",
                column: "PartnersIdsDocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_companies_documents_PartnersAttorneysDocumentId",
                table: "companies");

            migrationBuilder.DropForeignKey(
                name: "FK_companies_documents_PartnersIdsDocumentId",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_PartnersAttorneysDocumentId",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_companies_PartnersIdsDocumentId",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "HasManyPartners",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "PartnersAttorneysDocumentId",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "PartnersIdsDocumentId",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "PartnersText",
                table: "companies");
        }
    }
}
