#requires -Version 7.0
# ============================================================================
# Pruebas de humo del Sprint 1 (tarea S1-J10) — SIHA-IA
# Ejecuta el recorrido US-01 a US-06 contra el API Gateway y verifica los
# códigos HTTP y cuerpos esperados según los criterios de aceptación.
#
# Uso:
#   pwsh -File scripts/smoke-sprint1.ps1
#   pwsh -File scripts/smoke-sprint1.ps1 -BaseUrl http://localhost:5000
# ============================================================================

param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$AdminCorreo = "admin@uta.edu.ec",
    [string]$AdminPassword = $env:SIHA_ADMIN_PASSWORD
)

if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
    throw "Configura SIHA_ADMIN_PASSWORD o proporciona -AdminPassword."
}

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
    # PS7 conserva código y cuerpo de 4xx/5xx sin HttpWebResponse.GetResponseStream().
    # https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/invoke-webrequest#-skiphttperrorcheck
    $params = @{ Method = $Method; Uri = $uri; SkipHttpErrorCheck = $true; ErrorAction = 'Stop'; TimeoutSec = 30 }
    if ($Headers) { $params.Headers = $Headers }
    if ($Body) {
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }

    try {
        $resp = Invoke-WebRequest @params
        return @{ Codigo = [int]$resp.StatusCode; Cuerpo = $resp.Content }
    }
    catch {
        return @{ Codigo = 0; Cuerpo = $_.Exception.Message }
    }
}

function Verificar {
    param(
        [string]$Escenario,
        [string]$Historia,
        [int]$Esperado,
        $Respuesta,
        [scriptblock]$ValidarCuerpo
    )

    $obtenido = $Respuesta.Codigo
    $ok = ($obtenido -eq $Esperado)
    $detalle = ''
    if ($ok -and $ValidarCuerpo) {
        try {
            $json = ConvertFrom-Json -InputObject $Respuesta.Cuerpo -NoEnumerate -ErrorAction Stop
            $ok = (& $ValidarCuerpo $json) -eq $true
            if (-not $ok) { $detalle = 'El cuerpo no cumple el contrato esperado.' }
        } catch {
            $ok = $false
            $detalle = 'JSON inválido o contrato de respuesta incumplido.'
        }
    }
    $global:Resultados += [pscustomobject]@{
        Escenario = $Escenario
        Historia  = $Historia
        Esperado  = $Esperado
        Obtenido  = $obtenido
        Estado    = if ($ok) { "OK" } else { "FALLO" }
        Detalle   = $detalle
    }
    $color = if ($ok) { "Green" } else { "Red" }
    $etiqueta = if ($ok) { "OK" } else { "FALLO" }
    Write-Host ("[{0}] {1} -> esperado {2}, obtenido {3} {4}" -f $etiqueta, $Escenario, $Esperado, $obtenido, $detalle) -ForegroundColor $color
}

Write-Host "`n=== SIHA-IA · Pruebas de humo del Sprint 1 ===`n" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl`n"

# --- 0) Salud de los servicios -------------------------------------------
Verificar "Salud del API Gateway" "US-04" 200 (Invoke-Api -Method GET -Path "/health") { param($j) $j.servicio -eq 'Siha.Gateway' -and $j.estado -eq 'OK' }

# --- 1) US-01: autenticación y RBAC --------------------------------------
$login = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{
    correoInstitucional = $AdminCorreo
    password            = $AdminPassword
}
Verificar "Login del administrador" "US-01" 200 $login { param($j) -not [string]::IsNullOrWhiteSpace($j.token) -and $j.usuario.rol -eq 'ADMINISTRADOR' }
if ($global:Resultados[-1].Estado -ne 'OK') { throw 'Login administrativo fallido; no se envían peticiones con un token vacío.' }

$tokenAdmin = $null
if ($login.Codigo -eq 200) {
    $tokenAdmin = ($login.Cuerpo | ConvertFrom-Json).token
}
$hAdmin = @{ Authorization = "Bearer $tokenAdmin" }

Verificar "Identidad del token (auth/yo)" "US-01" 200 (Invoke-Api -Method GET -Path "/api/auth/yo" -Headers $hAdmin) { param($j) $j.correo -eq $AdminCorreo -and $j.rol -eq 'ADMINISTRADOR' }
Verificar "Login con contraseña incorrecta" "US-01" 401 (Invoke-Api -Method POST -Path "/api/auth/login" -Body @{
    correoInstitucional = $AdminCorreo
    password            = "clave-incorrecta"
}) { param($j) $j.mensaje -match 'Credenciales' }

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
Verificar "Alta de usuario con rol OPERADOR" "US-01" 201 $altaOperador { param($j) $j.correoInstitucional -eq $correoOperador -and $j.rol -eq 'OPERADOR' -and $j.id -gt 0 }

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
Verificar "Login del operador" "US-01" 200 $loginOperador { param($j) -not [string]::IsNullOrWhiteSpace($j.token) -and $j.usuario.rol -eq 'OPERADOR' }
if ($global:Resultados[-1].Estado -ne 'OK') { throw 'Login del operador fallido; no se envían peticiones con un token vacío.' }

$tokenOperador = $null
if ($loginOperador.Codigo -eq 200) {
    $tokenOperador = ($loginOperador.Cuerpo | ConvertFrom-Json).token
}
$hOperador = @{ Authorization = "Bearer $tokenOperador" }

Verificar "Operador intenta listar usuarios" "US-01" 403 (Invoke-Api -Method GET -Path "/api/usuarios" -Headers $hOperador)

# --- 2) US-02: reglas y umbrales -----------------------------------------
$umbralesAntes = Invoke-Api -Method GET -Path '/api/reglas/parametros/CAU-P-011' -Headers $hAdmin
$validarParametros = { param($j) $j.rutaNormativa -eq 'CAU-P-011' -and $j.version -ge 1 -and $j.theta -ge 0 -and $j.theta -le 100 -and $j.delta -ge 0 -and $j.delta -le $j.theta -and $j.tau -ge 0 -and $j.tau -le 100 }
Verificar "Consulta de umbrales CAU-P-011" "US-02" 200 $umbralesAntes $validarParametros
$antes = $null
if ($global:Resultados[-1].Estado -eq 'OK') { $antes = $umbralesAntes.Cuerpo | ConvertFrom-Json }
Verificar 'Consulta de reglas con fuente y clasificación' 'US-02' 200 (Invoke-Api -Method GET -Path '/api/reglas' -Headers $hAdmin) {
    param($j) ($j -is [array]) -and $j.Count -gt 0 -and @($j | Where-Object { -not $_.codigo -or -not $_.version -or -not $_.fuente -or $_.esNormativa -isnot [bool] }).Count -eq 0
}
Verificar "Actualización inválida (delta > theta)" "US-02" 400 (Invoke-Api -Method PUT -Path "/api/reglas/parametros/CAU-P-011" -Headers $hAdmin -Body @{
    theta = 50; delta = 80; tau = 60
}) { param($j) -not [string]::IsNullOrWhiteSpace($j.mensaje) }
Verificar 'Umbrales sin mutación tras 400' 'US-02' 200 (Invoke-Api -Method GET -Path '/api/reglas/parametros/CAU-P-011' -Headers $hAdmin) {
    param($j) $null -ne $antes -and $j.version -eq $antes.version -and $j.theta -eq $antes.theta -and $j.delta -eq $antes.delta -and $j.tau -eq $antes.tau
}
$actualizacion = Invoke-Api -Method PUT -Path '/api/reglas/parametros/CAU-P-011' -Headers $hAdmin -Body @{ theta = 82; delta = 12; tau = 71 }
Verificar "Actualización válida de umbrales" "US-02" 200 $actualizacion {
    param($j) $null -ne $antes -and $j.version -eq ($antes.version + 1) -and $j.theta -eq 82 -and $j.delta -eq 12 -and $j.tau -eq 71
}
$versionActual = $null
if ($global:Resultados[-1].Estado -eq 'OK') { $versionActual = ($actualizacion.Cuerpo | ConvertFrom-Json).version }
Verificar "Operador intenta modificar umbrales" "US-02" 403 (Invoke-Api -Method PUT -Path "/api/reglas/parametros/CAU-P-011" -Headers $hOperador -Body @{
    theta = 80; delta = 10; tau = 70
})
Verificar 'Umbrales sin mutación tras 403' 'US-02' 200 (Invoke-Api -Method GET -Path '/api/reglas/parametros/CAU-P-011' -Headers $hAdmin) {
    param($j) $null -ne $versionActual -and $j.version -eq $versionActual -and $j.theta -eq 82 -and $j.delta -eq 12 -and $j.tau -eq 71
}

# --- 3) US-03: catálogo académico ----------------------------------------
Verificar "Listado de carreras" "US-03" 200 (Invoke-Api -Method GET -Path "/api/carreras" -Headers $hAdmin) { param($j) $j -is [array] }
Verificar "Listado de mallas" "US-03" 200 (Invoke-Api -Method GET -Path "/api/mallas" -Headers $hAdmin) { param($j) $j -is [array] }
Verificar "Listado de asignaturas" "US-03" 200 (Invoke-Api -Method GET -Path "/api/asignaturas" -Headers $hAdmin) { param($j) $j -is [array] }

# --- 4) US-04: enrutamiento del Gateway ----------------------------------
Verificar "Ruta desconocida en el Gateway" "US-04" 404 (Invoke-Api -Method GET -Path "/api/ruta-inexistente" -Headers $hAdmin)

# --- 5) US-05 / US-06: borrador -> PDF validado en el servidor -> envío -> bus ----
function Send-Pdf {
    param([string]$ExpedienteId, [byte[]]$Contenido, [string]$Nombre, $Headers)

    # HttpClient conserva cuerpo y estado también en respuestas de error.
    Add-Type -AssemblyName System.Net.Http
    $cliente = New-Object System.Net.Http.HttpClient
    $cliente.DefaultRequestHeaders.Authorization =
        New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $Headers.Authorization.Substring(7))
    $formulario = New-Object System.Net.Http.MultipartFormDataContent
    $archivo = New-Object System.Net.Http.ByteArrayContent(,$Contenido)
    $archivo.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/pdf")
    $formulario.Add($archivo, "archivo", $Nombre)
    try {
        $resp = $cliente.PostAsync("$BaseUrl/api/expedientes/$ExpedienteId/documentos", $formulario).Result
        return @{ Codigo = [int]$resp.StatusCode; Cuerpo = $resp.Content.ReadAsStringAsync().Result }
    }
    catch { return @{ Codigo = 0; Cuerpo = $_.Exception.Message } }
    finally { $formulario.Dispose(); $cliente.Dispose() }
}

$cuerpoExpediente = @{
    tipoTramite          = "INTERNO"
    rutaNormativa        = "CAU-P-011"
    carreraOrigenCodigo  = "SW"
    carreraDestinoCodigo = "TI"
    institucionOrigen    = "UTA"
    asignaturas          = @(@{
        codigoOrigen  = "SW101"
        nombreOrigen  = "Fundamentos de Programación"
        codigoDestino = "TI101"
        nota          = 18.5
    })
}

# El cliente ya no puede declarar rutas/hashes: el registro con "documentos" se rechaza.
$conDeclarados = $cuerpoExpediente.Clone()
$conDeclarados.documentos = @(@{ nombreArchivo = "x.pdf"; rutaAlmacenamiento = "/etc/passwd"; hashSha256 = "0"; tamanoBytes = 1 })
Verificar "Documentos declarados por el cliente" "US-05" 400 (Invoke-Api -Method POST -Path "/api/expedientes" -Headers $hOperador -Body $conDeclarados) { param($j) $j.mensaje -match 'no se declaran' }

$expediente = Invoke-Api -Method POST -Path "/api/expedientes" -Headers $hOperador -Body $cuerpoExpediente
Verificar "Registro de expediente como borrador (201)" "US-05" 201 $expediente { param($j) $j.id -and $j.correlationId -and $j.estado -eq 'PENDIENTE_DOCUMENTOS' -and $j.publicado -eq $false }

if ($expediente.Codigo -eq 201) {
    $borrador = $expediente.Cuerpo | ConvertFrom-Json
    Write-Host ("     Borrador {0} | estado {1} | correlacion {2}" -f $borrador.codigo, $borrador.estado, $borrador.correlationId) -ForegroundColor DarkGray

    Verificar "Envío sin documentos (422)" "US-05" 422 (Invoke-Api -Method POST -Path "/api/expedientes/$($borrador.id)/enviar" -Headers $hOperador) { param($j) $j.expedienteId -eq $borrador.id -and $j.estado -eq 'PENDIENTE_DOCUMENTOS' -and $j.detalles.Count -gt 0 }

    $noPdf = [System.Text.Encoding]::ASCII.GetBytes("esto no es un pdf")
    Verificar "Carga de un archivo que no es PDF (400)" "US-05" 400 (Send-Pdf $borrador.id $noPdf "falso.pdf" $hOperador) { param($j) -not [string]::IsNullOrWhiteSpace($j.mensaje) }

    $pdf = [System.Text.Encoding]::ASCII.GetBytes("%PDF-1.7`n1 0 obj << /Type /Catalog >> endobj`n%%EOF`n")
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $hashPdf = [BitConverter]::ToString($sha.ComputeHash($pdf)).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
    $carga = Send-Pdf $borrador.id $pdf 'record-academico.pdf' $hOperador
    $validarDocumento = { param($j) $j.id -and $j.rutaAlmacenamiento -and $j.hashSha256 -eq $hashPdf -and $j.tamanoBytes -eq $pdf.Length -and $j.estadoRecepcion -eq 'VALIDADO' }
    Verificar "Carga de PDF validado en el servidor (201)" "US-05" 201 $carga $validarDocumento
    $documentoId = $null
    if ($global:Resultados[-1].Estado -eq 'OK') { $documentoId = ($carga.Cuerpo | ConvertFrom-Json).id }
    Verificar "Reintento de la misma carga sin duplicar (200)" "US-05" 200 (Send-Pdf $borrador.id $pdf "record-academico.pdf" $hOperador) {
        param($j) $null -ne $documentoId -and $j.id -eq $documentoId -and (& $validarDocumento $j)
    }

    $envio = Invoke-Api -Method POST -Path "/api/expedientes/$($borrador.id)/enviar" -Headers $hOperador
    $validarEnvio = { param($j) $j.id -eq $borrador.id -and $j.correlationId -eq $borrador.correlationId -and $j.publicado -eq $true -and $j.estado -eq 'PENDIENTE_CONTENIDOS' }
    Verificar "Publicación confirmada por MsB (202 + cuerpo; no acredita recepción por C)" "US-06" 202 $envio $validarEnvio
    Verificar "Segundo envío idempotente (200, sin republicar)" "US-06" 200 (Invoke-Api -Method POST -Path "/api/expedientes/$($borrador.id)/enviar" -Headers $hOperador) $validarEnvio
    Verificar "Consulta del estado y referencia documental" "US-05" 200 (Invoke-Api -Method GET -Path "/api/expedientes/$($borrador.id)" -Headers $hOperador) {
        param($j) $j.id -eq $borrador.id -and $j.eventoPublicado -eq $true -and $j.correlationId -eq $borrador.correlationId -and @($j.documentos | Where-Object { $_.id -eq $documentoId -and $_.hashSha256 -eq $hashPdf -and $_.tamanoBytes -eq $pdf.Length }).Count -eq 1
    }
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
