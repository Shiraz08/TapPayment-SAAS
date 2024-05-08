using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TapPaymentIntegration.Migrations
{
    public partial class manualpaid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaidBy",
                table: "invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "invoices",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidBy",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "invoices");
        }
    }
}
