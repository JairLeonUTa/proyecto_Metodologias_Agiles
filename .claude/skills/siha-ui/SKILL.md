---
name: siha-ui
description: Reglas de interfaz y de negocio visibles de SIHA-IA (Homologa UTA). Úsala siempre que se cree, cambie o revise una pantalla, texto o componente en frontend/src, junto con la skill frontend-design.
---

# Reglas de interfaz de SIHA-IA

## Público y lenguaje
- Usuarios con pocos conocimientos técnicos: Administrador, Operador y Docente. El estudiante **no** usa el sistema.
- Todo en **español sencillo**, tuteo neutro o "usted" consistente con la pantalla existente.
- **Prohibido** mostrar: símbolos o letras griegas (θ, δ, τ, σ…), nombres de variables, códigos de estado HTTP,
  trazas, JSON, GUIDs, "null/undefined", nombres de eventos o de microservicios.
- Errores: siempre pasar por `mensajeDeError` / `extraerMensaje` de `frontend/src/api/httpClient.ts`.
  Cada mensaje dice **qué pasó y qué hacer** ("No se pudo leer el PDF. Revise que sea el certificado SIHA y súbalo de nuevo.").
- Fechas `dd/mm/aaaa`, porcentajes `80,0 %`, créditos como número entero.

## Stack y dónde tocar
- React 18 + Vite + Tailwind + TypeScript en `frontend/`.
- Páginas: `frontend/src/pages/SolicitudNuevaPage.tsx`, `SolicitudesPage.tsx`, `SolicitudDetallePage.tsx`,
  `ReglasPage.tsx`, `ReglasHomologacionPage.tsx`.
- Reutilizar `frontend/src/components/ui` (`Alerta`, `Boton`, `Campo`, `Cargador`, `Casilla`, `EntradaTexto`,
  `AreaTexto`, `EstadoVacio`, `Etiqueta`, `Icono`, `Modal`, `Selector`, `Tabla`, `Tarjeta`) y
  `EncabezadoPagina`. No crear un componente nuevo si uno existente sirve.
- Colores solo con tokens de `frontend/tailwind.config.js` (ver skill `frontend-design`).
- Tests junto al código (`*.test.ts(x)`) con Vitest + Testing Library. Todo cambio de regla visible lleva prueba.

## Estados de homologación (por materia)
La regla vigente: **los créditos bloquean, los contenidos solo aconsejan**. Créditos < 80 % → no se puede
homologar, sin votación. Contenidos < 80 % (con créditos ≥ 80 %) → el agente recomienda no homologar, pero deciden
los docentes (el profesor pudo dar temas que no están en el sílabo).

| Estado | Cuándo | Cómo se muestra | Acciones |
|---|---|---|---|
| `CUMPLE` | contenidos ≥ 80 % **y** créditos ≥ 80 % | `Etiqueta tono="exito"` ✔ "Cumple el 80 %" | Votan los dos docentes; homologada si ambos aprueban |
| `NO_CUMPLE` + `bloqueadaPorCreditos` | créditos < 80 % (ej. 2 frente a 3 = 66,67 %) | `Etiqueta tono="error"` ✘ "No se puede homologar" + "Los créditos no llegan al 80 %." | **Sin votación**: sin botones; queda `NO_HOMOLOGADA`; el Administrador tampoco puede decidirla |
| `NO_CUMPLE` sin bloqueo | créditos ≥ 80 % y contenidos < 80 % | "✘ No llega al 80 % · recomienda no homologar" | Votan los dos docentes; pueden homologarla |
| `CRITERIO_DOCENTE` | no hay sílabo para comparar | `Etiqueta tono="advertencia"` ! "A criterio del docente" + "Sin sílabo para comparar" | Los docentes deciden |

Si los dos docentes discrepan (`SIN_ACUERDO`), el Administrador da la decisión final con un comentario.

- El mínimo vive en `MINIMO` (Reglas de homologación) y **no se puede bajar de 80** en `ReglasHomologacionPage`;
  si alguien lo intenta, explicar "El mínimo no puede ser menor a 80 %".
- Profundidad y carga horaria se muestran **solo como dato** (tono `muted`), nunca como motivo de decisión.
- Si los créditos se calculan con horas (sílabo externo sin créditos), decirlo en una línea.
- Estado de la solicitud `PREPARANDO`: "El agente se está preparando", con `Cargador`; explicar que se reintenta solo.
- Lo que el usuario ve en la lista (`SolicitudesPage`) y en el detalle debe usar los mismos textos y tonos.

## Validación previa del certificado PDF (`SolicitudNuevaPage`)
- Antes de registrar, mostrar la `ListaVerificacion` con **una fila por sección** del certificado:
  ✔ verde "Sin problemas." o ✘ rojo con la lista de problemas en frases simples.
- Tipos de rechazo del servidor (422): `NO_ES_CERTIFICADO`, `CAMPO_VACIO`, `REGLA` → traducir a texto humano,
  nunca mostrar el código.
- Botón "Registrar" deshabilitado mientras haya algún ✘, con el motivo escrito junto al botón.
- Movilidad externa (ruta CAU-P-122): pedir el sílabo de la otra universidad por materia, indicando cuál falta.
- Sugerencias de nombre oficial: "¿Quiso decir …?" con un clic para aceptar.
- Iconos ✔/✘ deben ir con texto y usar tokens `positive`/`negative` (no `emerald-600`/`red-600` sueltos en código nuevo).

## Roles en la interfaz
- Administrador: todo, incluidas Reglas y Usuarios. Operador: registra solicitudes. Docente: solo sus solicitudes
  como revisor y su decisión; aviso "Nueva" en las recientes.
- Ocultar (no solo deshabilitar) lo que un rol no puede usar.

## Antes de dar por bueno un cambio
1. `npm run lint` y `npm test` en `frontend/` en verde.
2. Revisar en `http://localhost:5173` con Playwright como el rol afectado (1366×768 y 375 px).
3. Buscar en el diff textos técnicos, símbolos o colores sueltos.
