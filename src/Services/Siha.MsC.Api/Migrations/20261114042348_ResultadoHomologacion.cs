using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class ResultadoHomologacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RESULTADO_HOMOLOGACION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoExpediente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroCertificado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EstudianteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoTramite = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RutaNormativa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstitucionOrigen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CarreraOrigen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CarreraDestino = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroSolicitud = table.Column<int>(type: "int", nullable: false),
                    MateriasOrigen = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    MateriaDestino = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CodigoDestino = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreditosOrigen = table.Column<int>(type: "int", nullable: true),
                    CreditosDestino = table.Column<int>(type: "int", nullable: true),
                    PorcentajeCreditos = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    HorasOrigen = table.Column<int>(type: "int", nullable: true),
                    HorasDestino = table.Column<int>(type: "int", nullable: false),
                    PorcentajeContenidos = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PorcentajeProfundidad = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PorcentajeCargaHoraria = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MinimoExigido = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SinSilabo = table.Column<bool>(type: "bit", nullable: false),
                    EstimacionAgente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Aprobaciones = table.Column<int>(type: "int", nullable: false),
                    Rechazos = table.Column<int>(type: "int", nullable: false),
                    ResultadoFinal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AnalizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RESULTADO_HOMOLOGACION", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RESULTADO_HOMOLOGACION_ExpedienteId_NumeroSolicitud",
                table: "RESULTADO_HOMOLOGACION",
                columns: new[] { "ExpedienteId", "NumeroSolicitud" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RESULTADO_HOMOLOGACION_ResultadoFinal",
                table: "RESULTADO_HOMOLOGACION",
                column: "ResultadoFinal");

            migrationBuilder.CreateIndex(
                name: "IX_RESULTADO_HOMOLOGACION_TipoTramite",
                table: "RESULTADO_HOMOLOGACION",
                column: "TipoTramite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RESULTADO_HOMOLOGACION");
        }
    }
}
