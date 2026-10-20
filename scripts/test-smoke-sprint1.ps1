#requires -Version 7.0
# Regresión del cliente smoke con HTTP local real; no levanta SIHA ni toca sus datos.
$ErrorActionPreference = 'Stop'
$tokens = $null
$errores = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'smoke-sprint1.ps1'), [ref]$tokens, [ref]$errores)
if ($errores.Count) { throw "El smoke tiene errores de sintaxis: $errores" }
foreach ($funcion in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($funcion.Extent.Text))
}
$sonda = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$sonda.Start()
$puerto = $sonda.LocalEndpoint.Port
$sonda.Stop()
$BaseUrl = "http://127.0.0.1:$puerto"
$servidor = Start-Job -ArgumentList $puerto -ScriptBlock {
    param($puerto)
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $puerto)
    $listener.Start()
    'listo'
    try {
        $cliente = $listener.AcceptTcpClient()
        $stream = $cliente.GetStream()
        $lector = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
        while ($lector.ReadLine()) { }
        $json = '{"mensaje":"Credenciales inválidas"}'
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $cabecera = [System.Text.Encoding]::ASCII.GetBytes("HTTP/1.1 400 Bad Request`r`nContent-Type: application/json; charset=utf-8`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n`r`n")
        $stream.Write($cabecera)
        $stream.Write($bytes)
        $cliente.Dispose()
    } finally { $listener.Stop() }
}
try {
    $limite = [DateTime]::UtcNow.AddSeconds(20)
    while (-not $servidor.HasMoreData -and [DateTime]::UtcNow -lt $limite) { Start-Sleep -Milliseconds 50 }
    if (-not $servidor.HasMoreData) { throw 'El servidor HTTP de prueba no arrancó' }
    Receive-Job $servidor | Out-Null
    $respuesta = Invoke-Api -Method GET -Path '/error'
    if ($respuesta.Codigo -ne 400 -or ($respuesta.Cuerpo | ConvertFrom-Json).mensaje -ne 'Credenciales inválidas') {
        throw 'No se conservaron código y cuerpo del error HTTP bajo PowerShell 7'
    }
    $global:Resultados = @()
    Verificar 'JSON correcto' 'prueba' 200 @{ Codigo = 200; Cuerpo = '{"valor":75}' } { param($j) $j.valor -eq 75 }
    Verificar 'JSON inválido' 'prueba' 200 @{ Codigo = 200; Cuerpo = 'no-json' } { param($j) $j.valor -eq 75 }
    Verificar 'Cuerpo incorrecto' 'prueba' 200 @{ Codigo = 200; Cuerpo = '{"valor":65}' } { param($j) $j.valor -eq 75 }
    if (($global:Resultados.Estado -join ',') -ne 'OK,FALLO,FALLO') { throw 'El smoke acepta cuerpos inválidos o incorrectos' }
    Write-Host "Regresión smoke: HTTP 400 local y validación de cuerpos correctos (PowerShell $($PSVersionTable.PSVersion))"
} finally {
    Stop-Job $servidor
    Remove-Job $servidor -Force
}
