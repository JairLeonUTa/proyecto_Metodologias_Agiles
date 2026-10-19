using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Siha.MsA.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CARRERA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Facultad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARRERA", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PARAMETRO_EVALUACION",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RutaNormativa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Theta = table.Column<double>(type: "float", nullable: false),
                    Delta = table.Column<double>(type: "float", nullable: false),
                    Tau = table.Column<double>(type: "float", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ActualizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualizadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PARAMETRO_EVALUACION", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "REGLA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RutaNormativa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TipoTramite = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Expresion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fuente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EsNormativa = table.Column<bool>(type: "bit", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGLA", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ROL",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROL", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MALLA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarreraId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Fuente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RutaNormativa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Vigente = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MALLA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MALLA_CARRERA_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "CARRERA",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "USUARIO",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorreoInstitucional = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UltimoAccesoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIO", x => x.Id);
                    table.ForeignKey(
                        name: "FK_USUARIO_ROL_RolId",
                        column: x => x.RolId,
                        principalTable: "ROL",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ASIGNATURA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MallaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Creditos = table.Column<int>(type: "int", nullable: false),
                    Horas = table.Column<int>(type: "int", nullable: false),
                    Semestre = table.Column<int>(type: "int", nullable: false),
                    PlanCompleto = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASIGNATURA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ASIGNATURA_MALLA_MallaId",
                        column: x => x.MallaId,
                        principalTable: "MALLA",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UNIDAD_TEMATICA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Horas = table.Column<int>(type: "int", nullable: false),
                    Contenidos = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UNIDAD_TEMATICA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UNIDAD_TEMATICA_ASIGNATURA_AsignaturaId",
                        column: x => x.AsignaturaId,
                        principalTable: "ASIGNATURA",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ASIGNATURA_MallaId_Codigo",
                table: "ASIGNATURA",
                columns: new[] { "MallaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CARRERA_Codigo",
                table: "CARRERA",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MALLA_CarreraId_Version",
                table: "MALLA",
                columns: new[] { "CarreraId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PARAMETRO_EVALUACION_RutaNormativa_Version",
                table: "PARAMETRO_EVALUACION",
                columns: new[] { "RutaNormativa", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REGLA_Codigo_RutaNormativa_Version",
                table: "REGLA",
                columns: new[] { "Codigo", "RutaNormativa", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROL_Nombre",
                table: "ROL",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UNIDAD_TEMATICA_AsignaturaId_Numero",
                table: "UNIDAD_TEMATICA",
                columns: new[] { "AsignaturaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_CorreoInstitucional",
                table: "USUARIO",
                column: "CorreoInstitucional",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_RolId",
                table: "USUARIO",
                column: "RolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PARAMETRO_EVALUACION");

            migrationBuilder.DropTable(
                name: "REGLA");

            migrationBuilder.DropTable(
                name: "UNIDAD_TEMATICA");

            migrationBuilder.DropTable(
                name: "USUARIO");

            migrationBuilder.DropTable(
                name: "ASIGNATURA");

            migrationBuilder.DropTable(
                name: "ROL");

            migrationBuilder.DropTable(
                name: "MALLA");

            migrationBuilder.DropTable(
                name: "CARRERA");
        }
    }
}
