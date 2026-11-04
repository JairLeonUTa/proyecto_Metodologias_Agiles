# Genera dos PDFs ficticios de prueba (tramite INTERNO y EXTERNO) para el flujo de extraccion.
# Sin compresion a proposito: ExtractorPdfService solo lee literales Tj de streams planos.
# Uso: powershell -File scripts/generar-pdf-prueba.ps1 [-Salida <carpeta>]

param([string]$Salida = (Join-Path $env:USERPROFILE 'Downloads\documentos de prueba'))

function New-PdfPrueba {
    param([string]$Ruta, [string[]]$Lineas)

    # Cada linea de $Lineas ya viene como literal PDF: (texto). Sin parentesis internos.
    $stream = "BT`n/F1 10 Tf`n50 790 Td`n14 TL`n"
    foreach ($linea in $Lineas) {
        $stream += "$linea Tj`nT*`n"
    }
    $stream += "ET`n"

    $objetos = @(
        "<< /Type /Catalog /Pages 2 0 R >>"
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        "<< /Length $($stream.Length) >>`nstream`n$stream" + "endstream"
    )

    $doc = "%PDF-1.4`n"
    $offsets = New-Object System.Collections.Generic.List[int]
    for ($i = 0; $i -lt $objetos.Count; $i++) {
        $offsets.Add([System.Text.Encoding]::ASCII.GetByteCount($doc))
        $doc += "$($i + 1) 0 obj`n$($objetos[$i])`nendobj`n"
    }
    $posXref = [System.Text.Encoding]::ASCII.GetByteCount($doc)
    $doc += "xref`n0 $($objetos.Count + 1)`n0000000000 65535 f `n"
    foreach ($offset in $offsets) { $doc += ('{0:D10} 00000 n ' -f $offset) + "`n" }
    $doc += "trailer`n<< /Size $($objetos.Count + 1) /Root 1 0 R >>`nstartxref`n$posXref`n%%EOF`n"

    [System.IO.File]::WriteAllText($Ruta, $doc, [System.Text.Encoding]::ASCII)
}

$interno = @(
    '(DOCUMENTO FICTICIO PARA PRUEBAS - NO ES OFICIAL)'
    '(SOLICITUD DE HOMOLOGACION - TRAMITE INTERNO)'
    '(Universidad Tecnica de Ambato - Facultad de Ingenieria en Sistemas)'
    '(DATOS DEL ESTUDIANTE)'
    '(Nombres y apellidos: Maria Fernanda Chicaiza Toapanta)'
    '(Cedula: 1804567890)'
    '(Carrera de origen: Tecnologias de la Informacion)'
    '(Carrera de destino: Ingenieria de Software)'
    '(Institucion de origen: Universidad Tecnica de Ambato)'
    '(Periodo academico: 2026-1)'
    '(Tipo de tramite: INTERNO - movilidad interna de carrera)'
    '(ANTECEDENTES ACADEMICOS)'
    '(Asignatura: Estructuras de Datos)'
    '(Codigo: SW201)'
    '(Creditos: 4)'
    '(Horas totales: 192)'
    '(Unidad 1: Analisis de algoritmos y tipos abstractos de datos - 32 horas.)'
    '(Notacion O grande, complejidad temporal y espacial, operaciones y pruebas.)'
    '(Unidad 2: Estructuras lineales - 48 horas.)'
    '(Arreglos, listas enlazadas, pilas, colas circulares e implementaciones.)'
    '(Unidad 3: Arboles y estructuras jerarquicas - 56 horas.)'
    '(Arboles binarios, recorridos, arboles de busqueda y colas de prioridad.)'
    '(Unidad 4: Tablas hash y grafos - 56 horas.)'
    '(Funciones hash, colisiones, grafos, recorridos BFS y DFS, busqueda de rutas.)'
    '(FIRMA DEL ESTUDIANTE: Maria Fernanda Chicaiza Toapanta)'
    '(FIN DEL DOCUMENTO DE PRUEBA)'
)

$externo = @(
    '(DOCUMENTO FICTICIO PARA PRUEBAS - NO ES OFICIAL)'
    '(SOLICITUD DE HOMOLOGACION - TRAMITE EXTERNO)'
    '(Universidad Tecnica de Ambato - Facultad de Ingenieria en Sistemas)'
    '(DATOS DEL ESTUDIANTE)'
    '(Nombres y apellidos: Luis Andres Paredes Mera)'
    '(Cedula: 1712345678)'
    '(Carrera de origen: Tecnologias de la Informacion)'
    '(Carrera de destino: Ingenieria de Software)'
    '(Institucion de origen: Universidad Nacional de Loja)'
    '(Periodo academico: 2025-2)'
    '(Tipo de tramite: EXTERNO - creditos de otra institucion)'
    '(ANTECEDENTES ACADEMICOS)'
    '(Asignatura: Programacion Orientada a Objetos)'
    '(Codigo: LOJ201)'
    '(Creditos: 5)'
    '(Horas totales: 240)'
    '(Unidad 1: Fundamentos de la programacion orientada a objetos - 40 horas.)'
    '(Clases, objetos, atributos, metodos y encapsulamiento.)'
    '(Unidad 2: Herencia y polimorfismo - 48 horas.)'
    '(Jerarquias de clases, sobrescritura y enlace dinamico.)'
    '(Unidad 3: Interfaces, clases abstractas y composicion - 56 horas.)'
    '(Contratos, abstraccion, delegacion y bajo acoplamiento.)'
    '(Unidad 4: Colecciones, excepciones y persistencia - 96 horas.)'
    '(Listas, mapas, manejo de errores y acceso a base de datos.)'
    '(FIRMA DEL ESTUDIANTE: Luis Andres Paredes Mera)'
    '(FIN DEL DOCUMENTO DE PRUEBA)'
)

New-Item -ItemType Directory -Force -Path $Salida | Out-Null
$rutas = @{
    'Homologacion-INTERNA-SW201.pdf' = $interno
    'Homologacion-EXTERNA-LOJ201.pdf' = $externo
}

foreach ($nombre in $rutas.Keys) {
    $ruta = Join-Path $Salida $nombre
    New-PdfPrueba -Ruta $ruta -Lineas $rutas[$nombre]

    # Verificacion: mismos patrones que ExtractorPdfService sobre el texto Tj del PDF.
    $texto = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($ruta))
    $lineas = [regex]::Matches($texto, '\(((?:\\.|[^\\)])*)\)\s*Tj') |
        ForEach-Object { [regex]::Replace($_.Groups[1].Value, '\\([\\()])', '$1') }
    $plano = $lineas -join "`n"

    $creditos = [regex]::Match($plano, '(?i)(?:cr[eé]ditos?|cr[eé]d\.?)\s*[:=-]?\s*(\d{1,2})').Groups[1].Value
    $horas = [regex]::Match($plano, '(?i)(?:horas?(?:\s+totales)?|carga\s+horaria|duraci[oó]n)\s*[:=-]?\s*(\d{1,4})').Groups[1].Value
    $codigo = [regex]::Match($plano, '(?i)(?:c[oó]digo|cod\.?)\s*[:=-]?\s*([A-Z0-9\-_]{3,15})').Groups[1].Value
    $asignatura = [regex]::Match($plano, '(?i)(?:asignatura|materia|nombre\s+de\s+la\s+materia)\s*[:=-]?\s*([^\r\n]{3,80})').Groups[1].Value.Trim()
    $unidades = ([regex]::Matches($plano, '(?im)^\s*(?:unidad|cap[ií]tulo|tema)\s*(\d+|[IVXLCDM]+)[:.\-\s]+(.+)$')).Count

    Write-Host "$nombre -> codigo=$codigo creditos=$creditos horas=$horas unidades=$unidades"
    Write-Host "  asignatura=$asignatura"
    if (-not $codigo -or -not $creditos -or -not $horas -or $unidades -lt 1) {
        throw "Verificacion fallida: el extractor no reconoceria $nombre"
    }
}

Write-Host "PDFs generados en $Salida"
