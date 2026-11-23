---
name: frontend-design
description: Criterios de diseño visual para pantallas de SIHA-IA (sistema universitario institucional). Úsala antes de diseñar, revisar o pedir a Codex cualquier cambio de interfaz en frontend/, para evitar el aspecto genérico de "hecho por IA".
---

# Diseño de interfaz institucional (sin aspecto genérico)

Objetivo: pantallas sobrias, legibles y confiables, como un trámite oficial bien hecho. Nada de
"landing page de startup".

## Prohibido (señales de diseño genérico)
- Degradados morados/azules, "glassmorphism", sombras grandes y difusas, brillos.
- Emojis como iconos, ilustraciones decorativas, tarjetas dentro de tarjetas sin motivo.
- Todo centrado; textos en gris claro sobre blanco; botones redondos tipo píldora para todo.
- Títulos de marketing ("¡Potencia tu homologación!"). Usar verbos de trámite: "Registrar solicitud".
- Animaciones de entrada, rebotes, parallax, contadores animados.
- Colores sueltos (`text-red-600`, `bg-emerald-50`…) cuando existe un token semántico.

## Jerarquía visual
1. Una sola acción principal por pantalla (`Boton` variante primaria). Las demás, secundarias o enlaces.
2. Orden de lectura: título de página (`EncabezadoPagina`) → resumen del estado → detalle → acciones.
3. Lo que decide el trámite (veredicto, porcentajes) va arriba y grande; lo informativo
   (profundidad, carga horaria) va abajo y en tono `muted`.
4. Agrupar con espacio y con `rule`/`edge` finos, no con cajas de color.
5. Alinear a la izquierda; números en columnas alineados a la derecha y con `tabular-nums`.

## Tipografía
- Familia única: IBM Plex Sans (`font-sans` / `font-display`); `font-mono` solo para códigos
  (número de certificado, cédula).
- Escala corta: título 24–28 px semibold, subtítulo 18 px semibold, texto 15–16 px, ayuda 13–14 px.
- Máximo ~70 caracteres por línea en textos explicativos. Nunca texto por debajo de 13 px.
- Porcentajes con un decimal y coma decimal: `66,7 %` (espacio antes del %).

## Color y contraste
- Usar los tokens de `frontend/tailwind.config.js`: superficies `frame`/`raised`/`active`,
  texto `ink`/`muted`/`faint`, marca `institucional-*`, semánticos `positive`/`negative`/`warning`,
  etiquetas `pill-pos`/`pill-neg`/`pill-warn`/`pill-neutral`.
- Contraste mínimo WCAG AA (4,5:1 texto normal). `faint` solo para datos secundarios, nunca para
  instrucciones.
- El color nunca va solo: siempre acompañado de texto y, si cabe, icono (✔, ✘, !).
- Marca (`institucional`) para navegación y acción principal; semánticos solo para estados.

## Estados bien diferenciados
Cada estado se reconoce por **texto + color + icono**, y se ve igual en lista y detalle:
- Éxito/aprobado: `positive` / `pill-pos`, icono ✔.
- Bloqueado/rechazado: `negative` / `pill-neg`, icono ✘, explica el motivo en una frase.
- Requiere decisión humana: `warning` / `pill-warn`, icono !.
- En proceso/preparando: marca o neutro + `Cargador`, con texto de lo que pasa y qué esperar.
- Vacío: `EstadoVacio` con qué hacer a continuación (nunca una tabla vacía muda).
- Deshabilitado: explicar por qué está deshabilitado (texto cerca del botón), no solo atenuar.

## Microinteracciones sobrias
- Transiciones de 120–200 ms solo en color, borde y opacidad (`transition-colors`). Sin escalados.
- Foco visible siempre (`focus-visible:ring-2` con `institucional`). Todo usable con teclado.
- Botones muestran estado de carga (`Boton` con carga) y no se pueden pulsar dos veces.
- Confirmación (`Modal`) solo para acciones que no se pueden deshacer (votar, eliminar).
- Mensajes de éxito discretos (`Alerta`) que no tapen el contenido.

## Responsive y accesibilidad
- Debe funcionar a 1366×768 (laptop de oficina) y a 375 px sin scroll horizontal.
- Tablas anchas: en móvil pasar a lista de tarjetas o permitir scroll solo dentro de la tabla.
- Etiquetas reales en cada campo (`Campo`), `aria-live` para avisos que cambian.

## Lista de revisión rápida
- [ ] ¿Hay una sola acción principal evidente?
- [ ] ¿Se entiende el estado sin leer colores?
- [ ] ¿Solo tokens del tema, sin colores sueltos?
- [ ] ¿Texto en español sencillo y sin jerga? (ver skill `siha-ui`)
- [ ] ¿Se ve bien a 1366×768 y a 375 px? (comprobar con Playwright)
