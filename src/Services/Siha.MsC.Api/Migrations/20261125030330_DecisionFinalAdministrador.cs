using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsC.Api.Migrations
{
    /// <inheritdoc />
    public partial class DecisionFinalAdministrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DECISION_FINAL",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpedienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Materia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aprobada = table.Column<bool>(type: "bit", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CorreoAdministrador = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreAdministrador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DECISION_FINAL", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DECISION_FINAL_ExpedienteId_Materia",
                table: "DECISION_FINAL",
                columns: new[] { "ExpedienteId", "Materia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DECISION_FINAL");
        }
    }
}
