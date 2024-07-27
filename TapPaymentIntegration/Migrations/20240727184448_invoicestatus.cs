using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TapPaymentIntegration.Migrations
{
    public partial class invoicestatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceStatus",
                table: "invoices",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceStatus",
                table: "invoices");
        }
    }
}
