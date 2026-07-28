using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegalERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedRowVersionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "company_partners");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "company_amendments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "companies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "company_partners",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "company_amendments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "companies",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }
    }
}
