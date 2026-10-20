using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Siha.Contracts;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Security;

namespace Siha.MsA.Tests;

public class SeguridadTests
{
    private static JwtTokenService CrearServicioJwt() =>
        new(Options.Create(new JwtSettings
        {
            Key = "clave-de-pruebas-para-firma-JWT-con-256-bits-minimo-ok",
            Issuer = "SIHA-IA",
            Audience = "SIHA-IA-Clients",
            ExpiraMinutos = 60
        }));

    [Fact]
    public void El_hash_BCrypt_no_expone_la_contrasena_y_se_verifica()
    {
        var hasher = new BCryptPasswordHasher();

        var hash = hasher.Hash("UnitTestOnly123!");

        hash.Should().NotBe("UnitTestOnly123!");
        hash.Should().StartWith("$2");
        hasher.Verify("UnitTestOnly123!", hash).Should().BeTrue();
        hasher.Verify("otra-clave", hash).Should().BeFalse();
    }

    [Fact]
    public void El_hash_se_genera_con_sal_distinta_en_cada_ocasion()
    {
        var hasher = new BCryptPasswordHasher();

        var hash1 = hasher.Hash("UnitTestOnly123!");
        var hash2 = hasher.Hash("UnitTestOnly123!");

        hash1.Should().NotBe(hash2);
        hasher.Verify("UnitTestOnly123!", hash1).Should().BeTrue();
        hasher.Verify("UnitTestOnly123!", hash2).Should().BeTrue();
    }

    [Fact]
    public void El_token_JWT_incluye_el_rol_y_los_claims_de_identidad()
    {
        var servicio = CrearServicioJwt();
        var usuario = new Usuario
        {
            Id = 7,
            CorreoInstitucional = "admin@uta.edu.ec",
            NombreCompleto = "Administrador SIHA-IA",
            RolId = 1
        };

        var token = servicio.CrearToken(usuario, RolesSistema.Administrador, "1");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("SIHA-IA");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == RolesSistema.Administrador);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "admin@uta.edu.ec");
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }
}
