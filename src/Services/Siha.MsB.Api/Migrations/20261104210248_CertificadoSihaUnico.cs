using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class CertificadoSihaUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Creditos",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Horas",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Semestre",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstudianteIdentificacion",
                table: "EXPEDIENTE",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstudianteNombre",
                table: "EXPEDIENTE",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroCertificado",
                table: "EXPEDIENTE",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_NumeroCertificado",
                table: "EXPEDIENTE",
                column: "NumeroCertificado",
                unique: true,
                filter: "[NumeroCertificado] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_NumeroCertificado",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "Creditos",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "Horas",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "EstudianteIdentificacion",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "EstudianteNombre",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "NumeroCertificado",
                table: "EXPEDIENTE");
        }
    }
}
