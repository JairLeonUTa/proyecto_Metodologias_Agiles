using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsA.Api.Migrations
{
    /// <inheritdoc />
    public partial class AsignaturaActiva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activa",
                table: "ASIGNATURA",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activa",
                table: "ASIGNATURA");
        }
    }
}
