using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class CertificadoParaElAgente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaAprobacion",
                table: "EXTRACTION");

            migrationBuilder.DropColumn(
                name: "Nota",
                table: "EXTRACTION");

            migrationBuilder.DropColumn(
                name: "Semestre",
                table: "EXTRACTION");

            migrationBuilder.AddColumn<string>(
                name: "CarreraDestinoNombre",
                table: "ANALISIS_EXPEDIENTE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CarreraOrigenNombre",
                table: "ANALISIS_EXPEDIENTE",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CertificadoJson",
                table: "ANALISIS_EXPEDIENTE",
                type: "NVARCHAR(MAX)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarreraDestinoNombre",
                table: "ANALISIS_EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "CarreraOrigenNombre",
                table: "ANALISIS_EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "CertificadoJson",
                table: "ANALISIS_EXPEDIENTE");

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
        }
    }
}
