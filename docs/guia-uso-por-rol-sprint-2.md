# Guía de uso por rol — SIHA-IA (estado actual)

Esta guía describe las funciones disponibles en el código revisado para el Sprint 2. La publicación de un expediente **no equivale a una homologación**: el sistema extrae información y calcula indicadores de apoyo; el dictamen corresponde al Docente.

## Flujo general

1. El Operador registra la solicitud, selecciona la ruta académica y las materias, adjunta y valida los documentos.
2. Pulsa **Enviar a procesamiento**. Esto publica el expediente en el bus; no emite un dictamen.
3. El Microservicio C verifica las referencias de documentos y crea las extracciones. Los PDF con texto pueden aparecer para revisión; los escaneados quedan pendientes de OCR/LLM.
4. El Operador revisa y confirma los datos extraídos.
5. El Docente analiza la extracción confirmada y revisa créditos, cobertura de contenidos y precedentes para emitir su dictamen.

## Operador

### Registrar una solicitud

1. Abra **Registrar solicitud**.
2. Complete los datos del trámite, la carrera de origen y la carrera destino.
3. Seleccione las asignaturas. Para materias UTA, ingrese el código: el nombre se completa desde el catálogo asociado a la carrera de origen. Si no existe en el catálogo o es una materia externa, complete los datos manualmente.
4. Adjunte los documentos requeridos, revise sus datos y registre la solicitud. El formulario no solicita la nota del estudiante.
5. En el detalle del expediente, pulse **Enviar a procesamiento** cuando los requisitos estén completos.

### Seguir y procesar

- En **Bandeja de expedientes**, consulte estado, publicación, documentos y materias. Si la publicación falló, use **Reintentar publicación**.
- En **Bandeja de procesamiento**, actualice la lista y revise el estado y la confianza de cada extracción.
- Si está **Pendiente de confirmación**, abra la acción de revisión, complete o corrija créditos, horas y unidades, y confirme. La confirmación habilita el análisis.
- Si está **Pendiente OCR/LLM**, la interfaz indica que no puede confirmarse todavía. En la versión actual revisada, el procesamiento OCR/LLM automático no está completado; no espere que se resuelva solo.

## Docente

1. Abra **Bandeja de análisis**.
2. Seleccione una extracción **Confirmada** y pulse **Analizar**. Las extracciones que aún no están confirmadas solo permiten consultar su estado.
3. Revise el resultado de créditos, cobertura de contenidos, controles normativos, precedentes y trazabilidad.
4. Use esos datos como apoyo y emita el dictamen académico según el procedimiento institucional. El agente no homologa automáticamente ni reemplaza la decisión docente.

## Administrador

El Administrador dispone de las funciones de consulta/registro permitidas en las bandejas y, además:

- administra cuentas y roles en **Usuarios y roles**;
- importa el catálogo académico en **Importar catálogo**;
- puede consultar reglas, carreras, mallas y asignaturas.

Debe mantener actualizado el catálogo para que el autocompletado de códigos UTA funcione correctamente. La asignación de roles controla las opciones visibles y el acceso a las rutas.

## Estados y límites importantes

- **Publicado** significa que el evento se envió al bus; no significa que el expediente esté homologado ni que Ollama haya terminado.
- **Pendiente de contenidos** es un estado del expediente, distinto del estado de la extracción. Consulte **Bandeja de procesamiento** para saber si existe extracción.
- La extracción de texto y el análisis dependen de que los servicios estén ejecutándose y de que el documento pueda leerse.
- Los PDF escaneados requieren OCR/LLM. Esa ruta no está completa en la versión revisada; un estado pendiente no se soluciona esperando.
- Antes de ofrecer el flujo completo al cliente, verifique que el backend actualizado esté desplegado y que las pruebas de integración estén aprobadas. En la revisión actual, una prueba de análisis falló por falta de unidades de origen.

## Dónde acudir si algo no avanza

| Situación | Qué revisar |
|---|---|
| No aparece el expediente en procesamiento | Compruebe en Bandeja de expedientes que figura como publicado; actualice la bandeja. Si falló, reintente la publicación. |
| Aparece pendiente de confirmación | El Operador debe revisar y confirmar los campos obligatorios. |
| Aparece pendiente OCR/LLM | La ruta OCR/LLM no está disponible de extremo a extremo en la versión revisada; escale al equipo técnico. |
| El Docente no puede analizar | Verifique que la extracción esté confirmada y que incluya unidades de origen. |
| El código de materia no completa el nombre | Compruebe que el código pertenezca al catálogo de la carrera de origen; use captura manual si no está registrado. |
