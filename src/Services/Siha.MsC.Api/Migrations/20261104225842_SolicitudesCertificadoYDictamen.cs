using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class SolicitudesCertificadoYDictamen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaAprobacion",
                table: "EXTRACTION",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nota",
                table: "EXTRACTION",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Semestre",
                table: "EXTRACTION",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ANALISIS_EXPEDIENTE",
                columns: table => new
                {
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoExpediente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroCertificado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EstudianteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoTramite = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RutaNormativa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstitucionOrigen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CarreraOrigenCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CarreraDestinoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Revisor1Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Revisor1Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Revisor2Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Revisor2Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResultadoJson = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnalizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ANALISIS_EXPEDIENTE", x => x.ExpedienteId);
                });

            migrationBuilder.CreateTable(
                name: "DICTAMEN_DOCENTE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorreoDocente = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreDocente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecisionesJson = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DICTAMEN_DOCENTE", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DICTAMEN_DOCENTE_ExpedienteId_CorreoDocente",
                table: "DICTAMEN_DOCENTE",
                columns: new[] { "ExpedienteId", "CorreoDocente" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ANALISIS_EXPEDIENTE");

            migrationBuilder.DropTable(
                name: "DICTAMEN_DOCENTE");

            migrationBuilder.DropColumn(
                name: "FechaAprobacion",
                table: "EXTRACTION");

            migrationBuilder.DropColumn(
                name: "Nota",
                table: "EXTRACTION");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "EXTRACTION");
        }
    }
}
