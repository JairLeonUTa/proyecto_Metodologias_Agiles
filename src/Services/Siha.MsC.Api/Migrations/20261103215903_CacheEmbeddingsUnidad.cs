using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <summary>
    /// S2-S05: caché de embeddings EMBEDDING_UNIDAD. La migración Precedentes no actualizó el snapshot del
    /// modelo; el snapshot de esta migración ya incluye PRECEDENTE, pero la tabla no se vuelve a crear aquí.
    /// </summary>
    public partial class CacheEmbeddingsUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EMBEDDING_UNIDAD",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoAsignatura = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroUnidad = table.Column<int>(type: "int", nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HashContenido = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Dimensiones = table.Column<int>(type: "int", nullable: false),
                    Vector = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMBEDDING_UNIDAD", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EMBEDDING_UNIDAD_CodigoAsignatura_NumeroUnidad_Modelo",
                table: "EMBEDDING_UNIDAD",
                columns: new[] { "CodigoAsignatura", "NumeroUnidad", "Modelo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EMBEDDING_UNIDAD");
        }
    }
}
