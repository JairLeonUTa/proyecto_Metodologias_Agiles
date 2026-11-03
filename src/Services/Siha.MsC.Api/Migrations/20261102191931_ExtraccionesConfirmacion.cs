using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExtraccionesConfirmacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EXTRACTION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoAsignaturaOrigen = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NombreAsignaturaOrigen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Creditos = table.Column<int>(type: "int", nullable: true),
                    Horas = table.Column<int>(type: "int", nullable: true),
                    Confianza = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXTRACTION", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EXTRACTION_UNIT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExtraccionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Horas = table.Column<int>(type: "int", nullable: false),
                    Contenidos = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXTRACTION_UNIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EXTRACTION_UNIT_EXTRACTION_ExtraccionId",
                        column: x => x.ExtraccionId,
                        principalTable: "EXTRACTION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXTRACTION_DocumentoId",
                table: "EXTRACTION",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_EXTRACTION_ExpedienteId",
                table: "EXTRACTION",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_EXTRACTION_UNIT_ExtraccionId_Numero",
                table: "EXTRACTION_UNIT",
                columns: new[] { "ExtraccionId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EXTRACTION_UNIT");

            migrationBuilder.DropTable(
                name: "EXTRACTION");
        }
    }
}
