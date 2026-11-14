#!/usr/bin/env bash
# Corre todas las pruebas .NET con el SDK 8 (el mismo del CI) dentro de un contenedor.
# Útil cuando la máquina solo tiene .NET 9 o más nuevo: las pruebas de integración levantan
# ASP.NET 8 en memoria y no funcionan con otro runtime.
#
# Uso (desde la raíz del repositorio, con Docker encendido):
#   docker run --rm -v "$PWD:/src:ro" -v /var/run/docker.sock:/var/run/docker.sock \
#     -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal -e TESTCONTAINERS_RYUK_DISABLED=true \
#     mcr.microsoft.com/dotnet/sdk:8.0 bash /src/scripts/pruebas-dotnet8-docker.sh
#
# TESTCONTAINERS_RYUK_DISABLED: el limpiador de Testcontainers (Ryuk) no se alcanza desde dentro de
# otro contenedor; sin él, cada prueba apaga sus propios contenedores al terminar.
# Solo la suite de integración: agregar al final  Siha.Integracion.Tests
set -uo pipefail

# Copia limpia: sin bin/obj/node_modules del sistema anfitrión (tienen rutas de Windows).
mkdir -p /work
tar -C /src --exclude=./frontend/node_modules --exclude='*/bin' --exclude='*/obj' --exclude=./.git -cf - . | tar -C /work -xf -
cd /work

fallos=0
suites=("$@")
[ ${#suites[@]} -eq 0 ] && suites=(Siha.MsA.Tests Siha.MsB.Tests Siha.MsC.Tests Siha.Integracion.Tests)
for suite in "${suites[@]}"; do
  proyecto="tests/$suite"
  echo "=================== $proyecto ==================="
  if ! dotnet test "$proyecto" -c Release --nologo -v q; then
    fallos=$((fallos + 1))
  fi
done

echo "=================== RESUMEN: $fallos suite(s) con fallos ==================="
exit "$fallos"
