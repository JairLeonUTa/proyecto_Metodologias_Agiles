using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMatriculaDestino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroMatriculaDestino",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumeroMatriculaDestino",
                table: "EXPEDIENTE_ASIGNATURA");
        }
    }
}
