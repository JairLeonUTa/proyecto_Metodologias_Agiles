using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class SilabosExternosMovilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "EXPEDIENTE_DOC",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MateriaOrigen",
                table: "EXPEDIENTE_DOC",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropColumn(
                name: "MateriaOrigen",
                table: "EXPEDIENTE_DOC");
        }
    }
}
