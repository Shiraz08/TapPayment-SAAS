using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TapPaymentIntegration.Migrations
{
    public partial class IsVoidInvoice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoidInvoice",
                table: "recurringCharges",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVoidInvoice",
                table: "recurringCharges");
        }
    }
}
