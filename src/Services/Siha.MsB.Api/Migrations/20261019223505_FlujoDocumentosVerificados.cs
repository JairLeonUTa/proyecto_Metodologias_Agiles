using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <summary>
    /// Flujo de documentos verificados (Sprint 1, integración de documentos e infraestructura):
    ///  - SelloConcurrencia: token de concurrencia optimista para que un expediente no se publique dos veces.
    ///  - FechaUltimoIntentoUtc: detecta envíos abandonados en PUBLICACION_EN_CURSO.
    ///  - Índice por Estado para el reintento automático.
    ///  - Índice único filtrado (ExpedienteId, HashSha256) para documentos VALIDADO: sin duplicados por reintento.
    ///
    /// Nota: el proyecto no tenía ModelSnapshot; esta migración lo incorpora y solo aplica los cambios
    /// incrementales sobre el esquema creado por las migraciones anteriores.
    /// </summary>
    public partial class FlujoDocumentosVerificados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SelloConcurrencia",
                table: "EXPEDIENTE",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaUltimoIntentoUtc",
                table: "EXPEDIENTE",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_Estado",
                table: "EXPEDIENTE",
                column: "Estado");

            // Limpieza previa: si existieran duplicados VALIDADO del mismo contenido en un expediente
            // (permitidos por la versión anterior), se conserva el más antiguo y el resto pasa a RETIRADO.
            migrationBuilder.Sql(@"
WITH duplicados AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ExpedienteId, HashSha256 ORDER BY FechaCargaUtc, Id) AS fila
    FROM EXPEDIENTE_DOC
    WHERE EstadoRecepcion = 'VALIDADO'
)
UPDATE d SET EstadoRecepcion = 'RETIRADO', ObservacionValidacion = 'Duplicado retirado por la migración FlujoDocumentosVerificados.'
FROM EXPEDIENTE_DOC d INNER JOIN duplicados x ON x.Id = d.Id
WHERE x.fila > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_DOC_Unico_Validado",
                table: "EXPEDIENTE_DOC",
                columns: new[] { "ExpedienteId", "HashSha256" },
                unique: true,
                filter: "[EstadoRecepcion] = 'VALIDADO'");

            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_DOC_ExpedienteId",
                table: "EXPEDIENTE_DOC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_DOC_ExpedienteId",
                table: "EXPEDIENTE_DOC",
                column: "ExpedienteId");

            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_DOC_Unico_Validado",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_Estado",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "FechaUltimoIntentoUtc",
                table: "EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "SelloConcurrencia",
                table: "EXPEDIENTE");
        }
    }
}
