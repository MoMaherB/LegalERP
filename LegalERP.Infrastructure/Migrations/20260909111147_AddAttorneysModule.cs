using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttorneysModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM documents 
                WHERE ""Id"" IN (
                    SELECT ""AttorneyDocumentId"" 
                    FROM clients 
                    WHERE ""AttorneyDocumentId"" IS NOT NULL
                );
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_clients_documents_AttorneyDocumentId",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "IX_clients_AttorneyDocumentId",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "AttorneyDocumentId",
                table: "clients");

            migrationBuilder.CreateTable(
                name: "attorneys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttorneyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attorneys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attorneys_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "attorney_clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttorneyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attorney_clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attorney_clients_attorneys_AttorneyId",
                        column: x => x.AttorneyId,
                        principalTable: "attorneys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attorney_clients_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attorney_clients_AttorneyId_ClientId",
                table: "attorney_clients",
                columns: new[] { "AttorneyId", "ClientId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_attorney_clients_ClientId",
                table: "attorney_clients",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_attorneys_AttorneyNumber",
                table: "attorneys",
                column: "AttorneyNumber")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_attorneys_DocumentId",
                table: "attorneys",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attorney_clients");

            migrationBuilder.DropTable(
                name: "attorneys");

            migrationBuilder.AddColumn<Guid>(
                name: "AttorneyDocumentId",
                table: "clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_clients_AttorneyDocumentId",
                table: "clients",
                column: "AttorneyDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_clients_documents_AttorneyDocumentId",
                table: "clients",
                column: "AttorneyDocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
