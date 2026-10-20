#!/usr/bin/env bash
# Comprobación sin contenedores: nunca permite ejecutar comandos destructivos.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
TMP_TEST="$(mktemp -d)"
trap 'rm -rf "$TMP_TEST"' EXIT
export DOCKER_CALLS="$TMP_TEST/docker-calls"
cat > "$TMP_TEST/docker" <<'SH'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$DOCKER_CALLS"
exit 1
SH
chmod +x "$TMP_TEST/docker"
export PATH="$TMP_TEST:$PATH"

if (unset SIHA_SQL_PASSWORD; bash scripts/e2e-docker-compose.sh) >/dev/null 2>&1; then exit 1; fi
test ! -e "$DOCKER_CALLS"

export SIHA_SQL_PASSWORD='Aa1!sql-test' SIHA_RABBITMQ_PASSWORD='rabbit-test'
export SIHA_JWT_KEY='clave-de-prueba-no-utilizar-en-produccion'
export SIHA_ADMIN_PASSWORD=$'Aa1!"\x27\\$\nprueba'
if bash scripts/e2e-docker-compose.sh >/dev/null 2>&1; then exit 1; fi
test "$(cat "$DOCKER_CALLS")" = 'compose config --quiet'

# Ejecuta solo la preparación, nunca el arranque ni la limpieza del E2E.
source <(sed '/^SELLO=/,$d' scripts/e2e-docker-compose.sh | sed '/^docker compose config --quiet/d; /^RAIZ=/d; /^cd /d')
printf '%s' "$LOGIN_JSON" | node -e 'const assert=require("node:assert/strict");let s="";process.stdin.on("data",x=>s+=x);process.stdin.on("end",()=>{const x=JSON.parse(s);assert.equal(x.password,process.env.SIHA_ADMIN_PASSWORD);assert.equal(x.correoInstitucional,process.env.ADMIN_CORREO);});'
echo 'E2E config: variables obligatorias, fallo seguro y JSON de credenciales correctos'
