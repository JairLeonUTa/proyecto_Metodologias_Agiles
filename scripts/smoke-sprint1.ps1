# ============================================================================
# Pruebas de humo del Sprint 1 (tarea S1-J10) — SIHA-IA
# Ejecuta el recorrido US-01 a US-06 contra el API Gateway y verifica los
# códigos HTTP esperados según los criterios de aceptación.
#
# Uso:
#   pwsh -File scripts/smoke-sprint1.ps1
#   pwsh -File scripts/smoke-sprint1.ps1 -BaseUrl http://localhost:5000
# ============================================================================

param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$AdminCorreo = "admin@uta.edu.ec",
    [string]$AdminPassword = "Admin123*"
)

$ErrorActionPreference = "Continue"
$global:Resultados = @()

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        $Body = $null,
        $Headers = $null
    )

    $uri = "$BaseUrl$Path"
    $params = @{ Method = $Method; Uri = $uri; UseBasicParsing = $true }
    if ($Headers) { $params.Headers = $Headers }
    if ($Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }

    try {
        $resp = Invoke-WebRequest @params
        return @{ Codigo = [int]$resp.StatusCode; Cuerpo = $resp.Content }
    }
    catch {
        $ex = $_.Exception.Response
        if ($null -ne $ex) {
            $codigo = [int]$ex.StatusCode
            $lector = New-Object System.IO.StreamReader($ex.GetResponseStream())
            $texto = $lector.ReadToEnd()
            return @{ Codigo = $codigo; Cuerpo = $texto }
        }
        return @{ Codigo = 0; Cuerpo = $_.Exception.Message }
    }
}

function Verificar {
    param(
        [string]$Escenario,
        [string]$Historia,
        [int]$Esperado,
        $Respuesta
    )

    $obtenido = $Respuesta.Codigo
    $ok = ($obtenido -eq $Esperado)
    $global:Resultados += [pscustomobject]@{
        Escenario = $Escenario
        Historia  = $Historia
        Esperado  = $Esperado
        Obtenido  = $obtenido
        Estado    = if ($ok) { "OK" } else { "FALLO" }
    }
    $color = if ($ok) { "Green" } else { "Red" }
    $etiqueta = if ($ok) { "OK" } else { "FALLO" }
    Write-Host ("[{0}] {1} -> esperado {2}, obtenido {3}" -f $etiqueta, $Escenario, $Esperado, $obtenido) -ForegroundColor $color
}

Write-Host "`n=== SIHA-IA · Pruebas de humo del Sprint 1 ===`n" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl`n"

# --- 0) Salud de los servicios -------------------------------------------
Verificar "Salud del API Gateway" "US-04" 200 (Invoke-Api -Method GET -Path "/health")

# --- 1) US-01: autenticación y RBAC --------------------------------------
$login = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{
    correoInstitucional = $AdminCorreo
    password            = $AdminPassword
}
Verificar "Login del administrador" "US-01" 200 $login

$tokenAdmin = $null
if ($login.Codigo -eq 200) {
    $tokenAdmin = ($login.Cuerpo | ConvertFrom-Json).token
}
$hAdmin = @{ Authorization = "Bearer $tokenAdmin" }

Verificar "Identidad del token (auth/yo)" "US-01" 200 (Invoke-Api -Method GET -Path "/api/auth/yo" -Headers $hAdmin)
Verificar "Login con contraseña incorrecta" "US-01" 401 (Invoke-Api -Method POST -Path "/api/auth/login" -Body @{
    correoInstitucional = $AdminCorreo
    password            = "clave-incorrecta"
})

Verificar "Listado de usuarios sin token" "US-01" 401 (Invoke-Api -Method GET -Path "/api/usuarios")

$sufijo = Get-Random -Minimum 1000 -Maximum 9999
$correoOperador = "operador$sufijo@uta.edu.ec"
$claveOperador = "Operador123*"

$altaOperador = Invoke-Api -Method POST -Path "/api/usuarios" -Headers $hAdmin -Body @{
    correoInstitucional = $correoOperador
    nombreCompleto      = "Operador de Pruebas $sufijo"
    password            = $claveOperador
    rol                 = "OPERADOR"
    activo              = $true
}
Verificar "Alta de usuario con rol OPERADOR" "US-01" 201 $altaOperador

Verificar "Correo institucional duplicado" "US-01" 409 (Invoke-Api -Method POST -Path "/api/usuarios" -Headers $hAdmin -Body @{
    correoInstitucional = $correoOperador
    nombreCompleto      = "Duplicado"
    password            = $claveOperador
    rol                 = "OPERADOR"
})

$loginOperador = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{
    correoInstitucional = $correoOperador
    password            = $claveOperador
}
Verificar "Login del operador" "US-01" 200 $loginOperador

$tokenOperador = $null
if ($loginOperador.Codigo -eq 200) {
    $tokenOperador = ($loginOperador.Cuerpo | ConvertFrom-Json).token
}
$hOperador = @{ Authorization = "Bearer $tokenOperador" }

Verificar "Operador intenta listar usuarios" "US-01" 403 (Invoke-Api -Method GET -Path "/api/usuarios" -Headers $hOperador)

# --- 2) US-02: reglas y umbrales -----------------------------------------
Verificar "Consulta de umbrales CAU-P-011" "US-02" 200 (Invoke-Api -Method GET -Path "/api/reglas/parametros/CAU-P-011" -Headers $hAdmin)
Verificar "Actualización inválida (delta > theta)" "US-02" 400 (Invoke-Api -Method PUT -Path "/api/reglas/parametros/CAU-P-011" -Headers $hAdmin -Body @{
    theta = 50; delta = 80; tau = 60
})
Verificar "Actualización válida de umbrales" "US-02" 200 (Invoke-Api -Method PUT -Path "/api/reglas/parametros/CAU-P-011" -Headers $hAdmin -Body @{
    theta = 82; delta = 12; tau = 71
})
Verificar "Operador intenta modificar umbrales" "US-02" 403 (Invoke-Api -Method PUT -Path "/api/reglas/parametros/CAU-P-011" -Headers $hOperador -Body @{
    theta = 80; delta = 10; tau = 70
})

# --- 3) US-03: catálogo académico ----------------------------------------
Verificar "Listado de carreras" "US-03" 200 (Invoke-Api -Method GET -Path "/api/carreras" -Headers $hAdmin)
Verificar "Listado de mallas" "US-03" 200 (Invoke-Api -Method GET -Path "/api/mallas" -Headers $hAdmin)
Verificar "Listado de asignaturas" "US-03" 200 (Invoke-Api -Method GET -Path "/api/asignaturas" -Headers $hAdmin)

# --- 4) US-04: enrutamiento del Gateway ----------------------------------
Verificar "Ruta desconocida en el Gateway" "US-04" 404 (Invoke-Api -Method GET -Path "/api/ruta-inexistente" -Headers $hAdmin)

# --- 5) US-05 / US-06: expediente y publicación en el bus ----------------
$expediente = Invoke-Api -Method POST -Path "/api/expedientes" -Headers $hOperador -Body @{
    tipoTramite          = "INTERNO"
    rutaNormativa        = "CAU-P-011"
    carreraOrigenCodigo  = "SW"
    carreraDestinoCodigo = "TI"
    institucionOrigen    = "UTA"
    documentos           = @(@{
        nombreArchivo     = "record-academico.pdf"
        rutaAlmacenamiento = "/data/record-academico.pdf"
        hashSha256        = "0000000000000000000000000000000000000000000000000000000000000000"
        tamanoBytes       = 2048
    })
    asignaturas          = @(@{
        codigoOrigen  = "SW101"
        nombreOrigen  = "Fundamentos de Programación"
        codigoDestino = "TI101"
        nota          = 18.5
    })
}
Verificar "Registro de expediente (202 Accepted)" "US-06" 202 $expediente

if ($expediente.Codigo -eq 202) {
    $aceptado = $expediente.Cuerpo | ConvertFrom-Json
    Write-Host ("     Expediente {0} | estado {1} | correlacion {2} | publicado {3}" -f $aceptado.codigo, $aceptado.estado, $aceptado.correlationId, $aceptado.publicado) -ForegroundColor DarkGray
    Verificar "Consulta del estado del expediente" "US-05" 200 (Invoke-Api -Method GET -Path "/api/expedientes/$($aceptado.id)" -Headers $hOperador)
}

Verificar "Tipo de trámite inválido" "US-06" 400 (Invoke-Api -Method POST -Path "/api/expedientes" -Headers $hOperador -Body @{
    tipoTramite          = "INVALIDO"
    rutaNormativa        = "CAU-P-011"
    carreraOrigenCodigo  = "SW"
    carreraDestinoCodigo = "TI"
})

# --- Resumen -------------------------------------------------------------
Write-Host "`n=== Resumen ===" -ForegroundColor Cyan
$global:Resultados | Format-Table -AutoSize

$total = $global:Resultados.Count
$ok = ($global:Resultados | Where-Object { $_.Estado -eq "OK" }).Count
$colorResumen = if ($ok -eq $total) { "Green" } else { "Yellow" }
Write-Host ("Pruebas superadas: {0}/{1}" -f $ok, $total) -ForegroundColor $colorResumen

if ($ok -ne $total) { exit 1 }
