using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "company_partners",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "case_parties",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullNameEn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NationalIdNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NationalIdDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttorneyDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clients_documents_AttorneyDocumentId",
                        column: x => x.AttorneyDocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_clients_documents_NationalIdDocumentId",
                        column: x => x.NationalIdDocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_partners_ClientId",
                table: "company_partners",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_ClientId",
                table: "case_parties",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_AttorneyDocumentId",
                table: "clients",
                column: "AttorneyDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_FullName",
                table: "clients",
                column: "FullName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_FullNameEn",
                table: "clients",
                column: "FullNameEn")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_NationalIdDocumentId",
                table: "clients",
                column: "NationalIdDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_case_parties_clients_ClientId",
                table: "case_parties",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_company_partners_clients_ClientId",
                table: "company_partners",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_case_parties_clients_ClientId",
                table: "case_parties");

            migrationBuilder.DropForeignKey(
                name: "FK_company_partners_clients_ClientId",
                table: "company_partners");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropIndex(
                name: "IX_company_partners_ClientId",
                table: "company_partners");

            migrationBuilder.DropIndex(
                name: "IX_case_parties_ClientId",
                table: "case_parties");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "company_partners");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "case_parties");
        }
    }
}
