using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class HistorialYCarrerasCertificado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Creditos",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.AddColumn<int>(
                name: "SolicitudNumero",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "Horas",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.AddColumn<int>(
                name: "Nivel",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Aprobada",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsignaturaDestinoNombre",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodoAcademico",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarreraDestinoNombre",
                table: "EXPEDIENTE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarreraOrigenNombre",
                table: "EXPEDIENTE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aprobada",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "AsignaturaDestinoNombre",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "PeriodoAcademico",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropColumn(
                name: "CarreraDestinoNombre",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "CarreraOrigenNombre",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "SolicitudNumero",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.AddColumn<int>(
                name: "Semestre",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.AddColumn<int>(
                name: "Horas",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Creditos",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "int",
                nullable: true);
        }
    }
}
