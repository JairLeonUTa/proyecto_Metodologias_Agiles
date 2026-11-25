using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Siha.MsC.Api.Controllers;
using Siha.Contracts;

namespace Siha.MsC.Tests;

public class SolicitudesAnalisisDecisionFinalTests
{
    [Fact]
    public void Endpoint_de_decision_final_exige_rol_administrador()
    {
        var metodo = typeof(AnalisisAutomaticoController).GetMethod(nameof(AnalisisAutomaticoController.DecidirComoAdministrador));
        var authorize = metodo!.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(RolesSistema.Administrador);
    }
}
