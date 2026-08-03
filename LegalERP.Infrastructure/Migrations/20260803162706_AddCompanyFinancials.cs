using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFinancials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CaseId",
                table: "fee_transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "fee_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgreedFee",
                table: "companies",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommercialRegisterNumber",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalEntity",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_fee_transactions_CompanyId",
                table: "fee_transactions",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_fee_transactions_companies_CompanyId",
                table: "fee_transactions",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fee_transactions_companies_CompanyId",
                table: "fee_transactions");

            migrationBuilder.DropIndex(
                name: "IX_fee_transactions_CompanyId",
                table: "fee_transactions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "fee_transactions");

            migrationBuilder.DropColumn(
                name: "AgreedFee",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "CommercialRegisterNumber",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "LegalEntity",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "companies");

            migrationBuilder.AlterColumn<Guid>(
                name: "CaseId",
                table: "fee_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
