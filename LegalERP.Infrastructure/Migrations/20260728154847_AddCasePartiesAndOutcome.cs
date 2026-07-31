using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCasePartiesAndOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "case_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsOurClient = table.Column<bool>(type: "boolean", nullable: false),
                    NationalIdNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_parties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_parties_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_parties_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_CaseId",
                table: "case_parties",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_DocumentId",
                table: "case_parties",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_parties");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "cases");
        }
    }
}
