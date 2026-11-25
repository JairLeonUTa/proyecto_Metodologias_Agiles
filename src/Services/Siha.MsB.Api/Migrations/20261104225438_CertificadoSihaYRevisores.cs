using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class CertificadoSihaYRevisores : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "Revisor1Correo",
                table: "EXPEDIENTE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revisor1Nombre",
                table: "EXPEDIENTE",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revisor2Correo",
                table: "EXPEDIENTE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revisor2Nombre",
                table: "EXPEDIENTE",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_NumeroCertificado",
                table: "EXPEDIENTE",
                column: "NumeroCertificado",
                unique: true,
                filter: "[NumeroCertificado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_Revisor1Correo",
                table: "EXPEDIENTE",
                column: "Revisor1Correo");

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_Revisor2Correo",
                table: "EXPEDIENTE",
                column: "Revisor2Correo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_NumeroCertificado",
                table: "EXPEDIENTE");

            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_Revisor1Correo",
                table: "EXPEDIENTE");

            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_Revisor2Correo",
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

            migrationBuilder.DropColumn(
                name: "Revisor1Correo",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "Revisor1Nombre",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "Revisor2Correo",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "Revisor2Nombre",
                table: "EXPEDIENTE");
        }
    }
}
