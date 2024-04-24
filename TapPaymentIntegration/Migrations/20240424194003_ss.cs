using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TapPaymentIntegration.Migrations
{
    public partial class ss : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "subscriptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstInvoice",
                table: "invoices",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recaptchaToken",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "IsFirstInvoice",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "recaptchaToken",
                table: "AspNetUsers");
        }
    }
}
