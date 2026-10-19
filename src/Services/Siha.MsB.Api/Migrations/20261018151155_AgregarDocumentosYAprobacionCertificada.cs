using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsB.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDocumentosYAprobacionCertificada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoRecepcion",
                table: "EXPEDIENTE_DOC",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VALIDADO");

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "EXPEDIENTE_DOC",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NombreAlmacenado",
                table: "EXPEDIENTE_DOC",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObservacionValidacion",
                table: "EXPEDIENTE_DOC",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaAprobacionCertificada",
                table: "EXPEDIENTE_ASIGNATURA",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXPEDIENTE_DOC_HashSha256",
                table: "EXPEDIENTE_DOC",
                column: "HashSha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EXPEDIENTE_DOC_HashSha256",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropColumn(
                name: "EstadoRecepcion",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropColumn(
                name: "NombreAlmacenado",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropColumn(
                name: "ObservacionValidacion",
                table: "EXPEDIENTE_DOC");

            migrationBuilder.DropColumn(
                name: "FechaAprobacionCertificada",
                table: "EXPEDIENTE_ASIGNATURA");
        }
    }
}
