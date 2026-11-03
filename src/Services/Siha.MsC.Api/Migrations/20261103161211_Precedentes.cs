using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class Precedentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PRECEDENTE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resolucion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AsignaturasOrigenJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AsignaturaDestino = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InstitucionOrigen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRECEDENTE", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PRECEDENTE_AsignaturaDestino_InstitucionOrigen",
                table: "PRECEDENTE",
                columns: new[] { "AsignaturaDestino", "InstitucionOrigen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PRECEDENTE");
        }
    }
}
