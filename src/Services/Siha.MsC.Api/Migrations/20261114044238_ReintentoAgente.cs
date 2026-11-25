using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReintentoAgente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Intentos",
                table: "ANALISIS_EXPEDIENTE",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProximoIntentoUtc",
                table: "ANALISIS_EXPEDIENTE",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ANALISIS_EXPEDIENTE_Estado_ProximoIntentoUtc",
                table: "ANALISIS_EXPEDIENTE",
                columns: new[] { "Estado", "ProximoIntentoUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ANALISIS_EXPEDIENTE_Estado_ProximoIntentoUtc",
                table: "ANALISIS_EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "Intentos",
                table: "ANALISIS_EXPEDIENTE");

            migrationBuilder.DropColumn(
                name: "ProximoIntentoUtc",
                table: "ANALISIS_EXPEDIENTE");
        }
    }
}
