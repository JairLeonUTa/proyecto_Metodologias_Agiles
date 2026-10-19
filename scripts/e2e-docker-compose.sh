#!/usr/bin/env bash
# ============================================================================
# SIHA-IA · Recorrido extremo a extremo con Docker Compose DESDE CERO (Sprint 1)
#
# Qué comprueba (todo con infraestructura real, sin simulaciones):
#   1. Arranque limpio: docker compose down -v + up --build (volúmenes vacíos).
#   2. Migraciones reales de DB A y DB B en SQL Server (__EFMigrationsHistory).
#   3. Gateway + JWT: login en MsA y registro del expediente en MsB a través de Ocelot.
#   4. Flujo documental: borrador -> PDF validado en el servidor -> envío -> RabbitMQ.
#   5. Recepción del mensaje por el suscriptor de verificación con el mismo CorrelationId.
#   6. Almacenamiento persistente: el PDF sobrevive a la recreación del contenedor ms-b.
#   7. Caída del bus (docker compose stop rabbitmq) y recuperación automática conservando el CorrelationId.
#   8. Frontend (Nginx) sirviendo la SPA y reenviando una carga de ~2 MB al Gateway.
#
# Uso (Linux, macOS o Git Bash en Windows; requiere Docker y curl):
#   bash scripts/e2e-docker-compose.sh            # recorrido completo y evidencia
#   CONSERVAR=1 bash scripts/e2e-docker-compose.sh # no apaga los contenedores al terminar
#   CONSTRUIR=0 bash scripts/e2e-docker-compose.sh # reutiliza las imágenes ya construidas
#
# Deja la evidencia en docs/evidencias/e2e-compose-<fecha>.md (y el log completo en .log).
# ============================================================================
set -uo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"

GATEWAY="${GATEWAY:-http://localhost:5000}"
FRONTEND="${FRONTEND:-http://localhost:5173}"
ADMIN_CORREO="${ADMIN_CORREO:-admin@uta.edu.ec}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-Admin123*}"
SQL_PASSWORD="SihaAdmin2026!"
SELLO="$(date -u +%Y%m%d-%H%M%S)"
DIR_EVIDENCIA="$RAIZ/docs/evidencias"
EVIDENCIA="$DIR_EVIDENCIA/e2e-compose-$SELLO.md"
LOG="$DIR_EVIDENCIA/e2e-compose-$SELLO.log"
TMP="$(mktemp -d)"
mkdir -p "$DIR_EVIDENCIA"

# Git Bash convierte rutas tipo /opt/... en rutas de Windows al pasarlas a docker; se desactiva.
export MSYS_NO_PATHCONV=1

OK=0
FALLOS=0
declare -a FILAS=()

log() { printf '%s %s\n' "$(date -u +%H:%M:%S)" "$*" | tee -a "$LOG"; }

registrar() { # registrar <resultado OK|FALLO> <comprobación> <detalle>
  local estado="$1" nombre="$2" detalle="${3:-}"
  if [[ "$estado" == "OK" ]]; then OK=$((OK + 1)); log "  [OK]    $nombre"; else FALLOS=$((FALLOS + 1)); log "  [FALLO] $nombre :: $detalle"; fi
  detalle="${detalle//|/\\|}"
  FILAS+=("| $estado | $nombre | ${detalle//$'\n'/ } |")
}

comprobar() { # comprobar <nombre> <esperado> <obtenido> [detalle]
  if [[ "$2" == "$3" ]]; then registrar OK "$1" "${4:-$3}"; else registrar FALLO "$1" "esperado '$2', obtenido '$3'. ${4:-}"; fi
}

campo() { # campo <json> <nombre>: extrae un valor simple (texto, número o booleano) sin depender de jq
  printf '%s' "$1" | tr -d '\n' | sed -nE "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"?([^\",}]*)\"?.*/\1/p" | head -n1
}

http() { # http <método> <url> [args curl...] -> imprime "código cuerpo" en $TMP/cuerpo
  local metodo="$1" url="$2"; shift 2
  curl -s -o "$TMP/cuerpo" -w '%{http_code}' -X "$metodo" "$url" "$@"
}

sql() { # sql <contenedor> <base> <consulta>
  docker exec "$1" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SQL_PASSWORD" -d "$2" -h -1 -W -Q "SET NOCOUNT ON; $3" 2>&1 | tr -d '\r' | sed '/^$/d' | head -n1
}

esperar() { # esperar <descripción> <segundos> <comando...>
  local descripcion="$1" limite="$2"; shift 2
  local inicio=$SECONDS
  until "$@" >/dev/null 2>&1; do
    if (( SECONDS - inicio > limite )); then log "  Tiempo agotado esperando: $descripcion"; return 1; fi
    sleep 3
  done
  log "  Listo: $descripcion ($((SECONDS - inicio)) s)"
}

crear_pdf() { # crear_pdf <ruta> <bytes de relleno>
  { printf '%%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\n%% '; head -c "$2" /dev/zero | tr '\0' 'x'; printf '\n%%%%EOF\n'; } > "$1"
}

expediente_json() {
  printf '{"tipoTramite":"EXTERNO","rutaNormativa":"CAU-P-122","carreraOrigenCodigo":"SW","carreraDestinoCodigo":"TI","institucionOrigen":"Universidad de prueba E2E","asignaturas":[{"codigoOrigen":"SW101","nombreOrigen":"Fundamentos de Programacion","codigoDestino":"TI101","nota":9.5,"fechaAprobacionCertificada":"2025-07-15"}]}'
}

limpiar() {
  rm -rf "$TMP"
  if [[ "${CONSERVAR:-0}" != "1" ]]; then
    log "Apagando el entorno (docker compose down -v)…"
    docker compose down -v --remove-orphans >>"$LOG" 2>&1
  fi
}
trap limpiar EXIT

# ---------------------------------------------------------------------------
log "== 1. Arranque desde cero =="
docker version --format 'Docker {{.Server.Version}}' 2>&1 | tee -a "$LOG"
docker compose version 2>&1 | tee -a "$LOG"
docker compose down -v --remove-orphans >>"$LOG" 2>&1
if [[ "${CONSTRUIR:-1}" == "1" ]]; then ARGS_UP=(-d --build); else ARGS_UP=(-d); fi
if docker compose up "${ARGS_UP[@]}" >>"$LOG" 2>&1; then registrar OK "docker compose up ${ARGS_UP[*]} (volúmenes vacíos)"; else registrar FALLO "docker compose up ${ARGS_UP[*]}" "ver $LOG"; exit 1; fi

# Basta con que Ocelot responda y reenvíe a ms-a (un login vacío devuelve 400, que también vale).
esperar "Gateway respondiendo" 300 bash -c "curl -s -o /dev/null -w '%{http_code}' -X POST '$GATEWAY/api/auth/login' -H 'Content-Type: application/json' -d '{}' | grep -qE '^(2|4)[0-9][0-9]$'" || true
esperar "ms-a sano" 300 bash -c "docker inspect -f '{{.State.Health.Status}}' siha-ms-a | grep -q healthy"
esperar "ms-b sano" 300 bash -c "docker inspect -f '{{.State.Health.Status}}' siha-ms-b | grep -q healthy"
esperar "login del administrador" 180 bash -c "curl -sf -o /dev/null -X POST '$GATEWAY/api/auth/login' -H 'Content-Type: application/json' -d '{\"correoInstitucional\":\"$ADMIN_CORREO\",\"password\":\"$ADMIN_PASSWORD\"}'"
docker compose ps >>"$LOG" 2>&1

log "== 2. Migraciones reales =="
MIG_A="$(sql siha-sqlserver-a SihaDbA 'SELECT COUNT(*) FROM __EFMigrationsHistory')"
MIG_B="$(sql siha-sqlserver-b SihaDbB 'SELECT COUNT(*) FROM __EFMigrationsHistory')"
ULT_B="$(sql siha-sqlserver-b SihaDbB 'SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC')"
[[ "${MIG_A:-0}" =~ ^[0-9]+$ && "$MIG_A" -ge 1 ]] && registrar OK "DB A migrada en SQL Server real" "$MIG_A migración(es)" || registrar FALLO "DB A migrada" "$MIG_A"
comprobar "DB B migrada en SQL Server real (3 migraciones)" "3" "$MIG_B" "última: $ULT_B"
comprobar "Índice único filtrado de documentos en DB B" "1" "$(sql siha-sqlserver-b SihaDbB "SELECT COUNT(*) FROM sys.indexes WHERE name='IX_EXPEDIENTE_DOC_Unico_Validado' AND has_filter=1")"

log "== 3. Autenticación a través del Gateway =="
CODIGO="$(http POST "$GATEWAY/api/auth/login" -H 'Content-Type: application/json' -d "{\"correoInstitucional\":\"$ADMIN_CORREO\",\"password\":\"$ADMIN_PASSWORD\"}")"
TOKEN="$(campo "$(cat "$TMP/cuerpo")" token)"
comprobar "Login vía Gateway (JWT emitido por MsA)" "200" "$CODIGO"
AUT=(-H "Authorization: Bearer $TOKEN")

log "== 4. Flujo documental =="
CODIGO="$(http POST "$GATEWAY/api/expedientes" "${AUT[@]}" -H 'Content-Type: application/json' -d '{"tipoTramite":"INTERNO","rutaNormativa":"CAU-P-011","carreraOrigenCodigo":"SW","carreraDestinoCodigo":"TI","documentos":[{"nombreArchivo":"x.pdf","rutaAlmacenamiento":"/etc/passwd","hashSha256":"0","tamanoBytes":1}]}')"
comprobar "Rechazo de documentos declarados por el cliente" "400" "$CODIGO"

CODIGO="$(http POST "$GATEWAY/api/expedientes" "${AUT[@]}" -H 'Content-Type: application/json' -d "$(expediente_json)")"
CUERPO="$(cat "$TMP/cuerpo")"; EXP_ID="$(campo "$CUERPO" id)"; CORR="$(campo "$CUERPO" correlationId)"
comprobar "Registro del borrador vía Gateway" "201" "$CODIGO" "id=$EXP_ID estado=$(campo "$CUERPO" estado) correlationId=$CORR"

CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP_ID/enviar" "${AUT[@]}")"
comprobar "Envío sin documentos bloqueado" "422" "$CODIGO"

printf 'no soy un pdf' > "$TMP/falso.pdf"
CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP_ID/documentos" "${AUT[@]}" -F "archivo=@$TMP/falso.pdf;type=application/pdf")"
comprobar "Carga rechazada (firma %PDF ausente)" "400" "$CODIGO" "$(campo "$(cat "$TMP/cuerpo")" mensaje)"

crear_pdf "$TMP/plan.pdf" 4096
HASH_LOCAL="$(sha256sum "$TMP/plan.pdf" | cut -d' ' -f1)"
CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP_ID/documentos" "${AUT[@]}" -F "archivo=@$TMP/plan.pdf;type=application/pdf")"
CUERPO="$(cat "$TMP/cuerpo")"; DOC_ID="$(campo "$CUERPO" id)"; RUTA="$(campo "$CUERPO" rutaAlmacenamiento)"; HASH_SERV="$(campo "$CUERPO" hashSha256)"
comprobar "Carga de PDF real vía Gateway" "201" "$CODIGO" "documento=$DOC_ID ruta=$RUTA"
comprobar "SHA-256 calculado por el servidor = SHA-256 local" "$HASH_LOCAL" "$HASH_SERV"

CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP_ID/documentos" "${AUT[@]}" -F "archivo=@$TMP/plan.pdf;type=application/pdf")"
comprobar "Reintento de la misma carga: 200 sin duplicar" "200" "$CODIGO" "id devuelto=$(campo "$(cat "$TMP/cuerpo")" id)"
comprobar "Una sola fila VALIDADO en DB B" "1" "$(sql siha-sqlserver-b SihaDbB "SELECT COUNT(*) FROM EXPEDIENTE_DOC WHERE ExpedienteId='$EXP_ID' AND EstadoRecepcion='VALIDADO'")"

HASH_DISCO="$(docker exec siha-ms-b sha256sum "/app/archivos/$RUTA" 2>/dev/null | cut -d' ' -f1)"
comprobar "Archivo en el volumen siha-archivos con el mismo hash" "$HASH_LOCAL" "$HASH_DISCO"

CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP_ID/enviar" "${AUT[@]}")"
comprobar "Envío con documento verificado (202 Accepted)" "202" "$CODIGO" "estado=$(campo "$(cat "$TMP/cuerpo")" estado) publicado=$(campo "$(cat "$TMP/cuerpo")" publicado)"
CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP_ID/enviar" "${AUT[@]}")"
comprobar "Segundo envío idempotente (200, sin republicar)" "200" "$CODIGO"

log "== 5. Recepción en RabbitMQ =="
if esperar "mensaje recibido por el suscriptor de verificación" 60 bash -c "docker logs siha-ms-b 2>&1 | grep -q 'SolicitudCreadaEvent recibido.*ExpedienteId=$EXP_ID CorrelationId=$CORR'"; then
  registrar OK "Mensaje recibido en RabbitMQ con el mismo CorrelationId" "$(docker logs siha-ms-b 2>&1 | grep "ExpedienteId=$EXP_ID" | grep 'recibido' | head -n1 | cut -c1-220)"
else
  registrar FALLO "Mensaje recibido en RabbitMQ" "no apareció en los logs de ms-b"
fi
RECIBIDOS="$(docker logs siha-ms-b 2>&1 | grep -c "SolicitudCreadaEvent recibido.*ExpedienteId=$EXP_ID")"
comprobar "Exactamente un mensaje para el expediente" "1" "$RECIBIDOS"
INTERCAMBIO="$(curl -s -u siha:siha-rabbit-2026 'http://localhost:15672/api/exchanges/%2F/Siha.Contracts.Events:SolicitudCreadaEvent' | grep -o '"name":"Siha.Contracts.Events:SolicitudCreadaEvent"' | head -n1)"
[[ -n "$INTERCAMBIO" ]] && registrar OK "Intercambio del contrato declarado en RabbitMQ" "$INTERCAMBIO" || registrar FALLO "Intercambio del contrato en RabbitMQ" "no encontrado"

log "== 6. Persistencia del almacenamiento =="
docker compose up -d --force-recreate --no-deps ms-b >>"$LOG" 2>&1
esperar "ms-b recreado y sano" 240 bash -c "docker inspect -f '{{.State.Health.Status}}' siha-ms-b | grep -q healthy"
HASH_TRAS="$(docker exec siha-ms-b sha256sum "/app/archivos/$RUTA" 2>/dev/null | cut -d' ' -f1)"
comprobar "PDF intacto tras recrear el contenedor ms-b" "$HASH_LOCAL" "$HASH_TRAS"
CODIGO="$(http GET "$GATEWAY/api/expedientes/$EXP_ID" "${AUT[@]}")"
comprobar "Expediente consultable tras el reinicio" "200" "$CODIGO" "estado=$(campo "$(cat "$TMP/cuerpo")" estado)"

log "== 7. Caída y recuperación del bus =="
CODIGO="$(http POST "$GATEWAY/api/expedientes" "${AUT[@]}" -H 'Content-Type: application/json' -d "$(expediente_json)")"
CUERPO="$(cat "$TMP/cuerpo")"; EXP2="$(campo "$CUERPO" id)"; CORR2="$(campo "$CUERPO" correlationId)"
crear_pdf "$TMP/plan2.pdf" 2048
http POST "$GATEWAY/api/expedientes/$EXP2/documentos" "${AUT[@]}" -F "archivo=@$TMP/plan2.pdf;type=application/pdf" >/dev/null

docker compose stop rabbitmq >>"$LOG" 2>&1
log "  RabbitMQ DETENIDO ($(date -u +%H:%M:%S))"
CODIGO="$(http POST "$GATEWAY/api/expedientes/$EXP2/enviar" "${AUT[@]}" --max-time 120)"
CUERPO="$(cat "$TMP/cuerpo")"
comprobar "Con el bus caído: 202 y estado recuperable" "202 REGISTRADO_PENDIENTE_PUBLICACION" "$CODIGO $(campo "$CUERPO" estado)" "correlationId=$(campo "$CUERPO" correlationId)"
comprobar "CorrelationId conservado durante la caída" "$CORR2" "$(campo "$CUERPO" correlationId)"

docker compose start rabbitmq >>"$LOG" 2>&1
log "  RabbitMQ INICIADO ($(date -u +%H:%M:%S))"
if esperar "reintento automático publicó el expediente" 240 bash -c "curl -s -H 'Authorization: Bearer $TOKEN' '$GATEWAY/api/expedientes/$EXP2' | grep -q '\"estado\":\"PENDIENTE_CONTENIDOS\"'"; then
  CUERPO="$(curl -s "${AUT[@]}" "$GATEWAY/api/expedientes/$EXP2")"
  registrar OK "Recuperación automática tras reiniciar RabbitMQ" "estado=$(campo "$CUERPO" estado) intentos=$(campo "$CUERPO" intentosPublicacion) correlationId=$(campo "$CUERPO" correlationId)"
  comprobar "CorrelationId idéntico tras la recuperación" "$CORR2" "$(campo "$CUERPO" correlationId)"
else
  registrar FALLO "Recuperación automática tras reiniciar RabbitMQ" "el expediente no llegó a PENDIENTE_CONTENIDOS"
fi
if esperar "mensaje recuperado recibido" 90 bash -c "docker logs siha-ms-b 2>&1 | grep -q 'SolicitudCreadaEvent recibido.*ExpedienteId=$EXP2 CorrelationId=$CORR2'"; then
  registrar OK "Mensaje recibido tras la recuperación con el mismo CorrelationId" "$(docker logs siha-ms-b 2>&1 | grep "ExpedienteId=$EXP2" | grep 'recibido' | head -n1 | cut -c1-220)"
else
  registrar FALLO "Mensaje recibido tras la recuperación" "no apareció en los logs de ms-b"
fi

log "== 8. Frontend (Nginx) =="
comprobar "SPA servida por Nginx" "200" "$(curl -s -o /dev/null -w '%{http_code}' "$FRONTEND/")"
CODIGO="$(http POST "$FRONTEND/api/expedientes" "${AUT[@]}" -H 'Content-Type: application/json' -d "$(expediente_json)")"
EXP3="$(campo "$(cat "$TMP/cuerpo")" id)"
crear_pdf "$TMP/grande.pdf" 2097152
CODIGO="$(http POST "$FRONTEND/api/expedientes/$EXP3/documentos" "${AUT[@]}" -F "archivo=@$TMP/grande.pdf;type=application/pdf")"
comprobar "Carga de PDF de ~2 MB a través de Nginx -> Gateway -> MsB" "201" "$CODIGO"

# ---------------------------------------------------------------------------
TOTAL=$((OK + FALLOS))
{
  echo "# Evidencia E2E · Docker Compose desde cero"
  echo
  echo "- **Fecha (UTC):** $(date -u '+%Y-%m-%d %H:%M:%S')"
  echo "- **Equipo:** $(uname -s) $(uname -m) · $(docker version --format 'Docker {{.Server.Version}}' 2>/dev/null) · $(docker compose version --short 2>/dev/null)"
  echo "- **Commit:** $(git rev-parse --short HEAD 2>/dev/null || echo 'n/d')"
  echo "- **Tipo de prueba:** INFRAESTRUCTURA REAL (contenedores de docker-compose.yml: SQL Server A/B, RabbitMQ, ms-a, ms-b, gateway, frontend). Sin mocks ni bases en memoria."
  echo "- **Resultado:** $OK/$TOTAL comprobaciones superadas"
  echo
  echo "| Resultado | Comprobación | Detalle |"
  echo "|---|---|---|"
  printf '%s\n' "${FILAS[@]}"
  echo
  echo "Log completo: \`$(basename "$LOG")\`"
} > "$EVIDENCIA"

log "Evidencia: $EVIDENCIA"
log "Resultado: $OK/$TOTAL comprobaciones superadas"
[[ $FALLOS -eq 0 ]]
