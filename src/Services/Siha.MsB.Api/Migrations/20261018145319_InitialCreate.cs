using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EXPEDIENTE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TipoTramite = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RutaNormativa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CarreraOrigenCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CarreraDestinoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstitucionOrigen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventoPublicado = table.Column<bool>(type: "bit", nullable: false),
                    IntentosPublicacion = table.Column<int>(type: "int", nullable: false),
                    UltimoErrorPublicacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaCreacionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FechaPublicacionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXPEDIENTE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EXPEDIENTE_ASIGNATURA",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignaturaOrigenCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AsignaturaOrigenNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AsignaturaDestinoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NotaOrigen = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXPEDIENTE_ASIGNATURA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EXPEDIENTE_ASIGNATURA_EXPEDIENTE_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "EXPEDIENTE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EXPEDIENTE_DOC",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RutaAlmacenamiento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaCargaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXPEDIENTE_DOC", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EXPEDIENTE_DOC_EXPEDIENTE_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "EXPEDIENTE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_Codigo",
                table: "EXPEDIENTE",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_CorrelationId",
                table: "EXPEDIENTE",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_ASIGNATURA_ExpedienteId",
                table: "EXPEDIENTE_ASIGNATURA",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_DOC_ExpedienteId",
                table: "EXPEDIENTE_DOC",
                column: "ExpedienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EXPEDIENTE_ASIGNATURA");

            migrationBuilder.DropTable(
                name: "EXPEDIENTE_DOC");

            migrationBuilder.DropTable(
                name: "EXPEDIENTE");
        }
    }
}
