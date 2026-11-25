using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class SilabosExternosMovilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SilabosExternosJson",
                table: "ANALISIS_EXPEDIENTE",
                type: "NVARCHAR(MAX)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SilabosExternosJson",
                table: "ANALISIS_EXPEDIENTE");
        }
    }
}
