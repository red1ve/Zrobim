using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zrobim.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MasterProfileAddressVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_address_in_catalog",
                table: "masters",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "show_address_in_catalog",
                table: "masters");
        }
    }
}
