import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { extraccionesApi } from '../api';
import { EtapasExtraccion } from '../components/analisis/EtapasExtraccion';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, CargandoBloque, Etiqueta, Tarjeta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { ESTADOS_EXTRACCION } from '../types/analisis';
import { ROLES } from '../types/comunes';
import { camposVacios, etapasExtraccion, formatearDecimal, haceSegundos, presentarEstadoExtraccion } from '../utils/analisis';

/** Intervalo de sondeo mientras el PDF escaneado sigue en OCR/LLM. */
export const INTERVALO_SONDEO_MS = 10_000;

/**
 * Estado de la extracción ampliada OCR + LLM (S2-A05, US-10, mockup M-04).
 * Indicadores "OCR en curso", "LLM analizando" y "Validación operador requerida". Mientras el
 * expediente sigue en PENDIENTE_OCR_LLM la vista se actualiza sola cada 10 s.
 */
export function EstadoExtraccionPage(): JSX.Element {
  useTituloPagina('Estado de la extracción');
  const { id = '' } = useParams();
  const { tieneRol } = useAuth();
  const extraccion = usePeticion(() => extraccionesApi.obtener(id), [id]);
  const [cargadoEn, setCargadoEn] = useState(() => Date.now());
  const [ahora, setAhora] = useState(() => Date.now());

  const datos = extraccion.datos;
  const enOcr = datos?.estado === ESTADOS_EXTRACCION.PENDIENTE_OCR_LLM;

  useEffect(() => {
    if (datos) setCargadoEn(Date.now());
  }, [datos]);

  useEffect(() => {
    const reloj = window.setInterval(() => setAhora(Date.now()), 1000);
    return () => window.clearInterval(reloj);
  }, []);

  useEffect(() => {
    if (!enOcr) return undefined;
    const sondeo = window.setInterval(() => extraccion.recargar(), INTERVALO_SONDEO_MS);
    return () => window.clearInterval(sondeo);
  }, [enOcr, extraccion.recargar]); // eslint-disable-line react-hooks/exhaustive-deps

  if (extraccion.cargando && !datos) {
    return <CargandoBloque />;
  }

  if (extraccion.error && !datos) {
    return (
      <div className="space-y-4">
        <EncabezadoPagina titulo="Estado de la extracción ampliada" />
        <Alerta
          tono="error"
          titulo="No se pudo consultar el estado del PDF"
          accion={
            <Boton variante="secundario" tamano="sm" onClick={() => extraccion.recargar()}>
              Reintentar
            </Boton>
          }
        >
          {extraccion.error}
        </Alerta>
      </div>
    );
  }

  if (!datos) {
    return <CargandoBloque />;
  }

  const vacios = camposVacios(datos);
  const estado = presentarEstadoExtraccion(datos.estado);
  const puedeConfirmar =
    datos.estado === ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION && tieneRol(ROLES.OPERADOR, ROLES.ADMINISTRADOR);

  const filas: Array<{ campo: string; valor: string | null }> = [
    { campo: 'Asignatura', valor: datos.nombreAsignaturaOrigen || null },
    { campo: 'Código', valor: datos.codigoAsignaturaOrigen || null },
    { campo: 'Créditos', valor: datos.creditos === null ? null : String(datos.creditos) },
    { campo: 'Horas totales', valor: datos.horas === null ? null : String(datos.horas) },
    { campo: 'Unidades detectadas', valor: datos.unidades.length > 0 ? String(datos.unidades.length) : null },
    ...datos.unidades.map((unidad) => ({
      campo: `Horas de U.${unidad.numero}`,
      valor: unidad.horas ? String(unidad.horas) : null,
    })),
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Estado de la extracción ampliada"
        descripcion={`${datos.codigoAsignaturaOrigen} · ${datos.nombreAsignaturaOrigen || 'PDF en lectura'}`}
        acciones={<Etiqueta tono={estado.tono}>{estado.etiqueta}</Etiqueta>}
      />

      <Tarjeta>
        <EtapasExtraccion etapas={etapasExtraccion(datos)} />
      </Tarjeta>

      {enOcr ? (
        <Alerta tono="informacion" titulo="OCR en curso · LLM analizando">
          El PDF no tiene capa de texto. El Microservicio C lo lee con OCR y el modelo local estructura los datos; esta
          vista se actualiza cada 10 segundos.
        </Alerta>
      ) : null}

      {vacios.length > 0 && !enOcr ? (
        <Alerta tono="advertencia">
          <strong>
            {vacios.length} campo{vacios.length > 1 ? 's' : ''} con confianza baja
          </strong>{' '}
          ({vacios.join(', ')}). Quedan vacíos para que el operador los confirme; el sistema no los inventa.
        </Alerta>
      ) : null}

      {extraccion.error ? (
        <Alerta tono="advertencia">La última actualización falló: {extraccion.error}</Alerta>
      ) : null}

      <Tarjeta
        titulo="Datos leídos hasta ahora"
        descripcion={`Confianza global ${formatearDecimal(datos.confianza)}`}
        compacta
        acciones={
          <span className="text-xs text-faint" aria-live="polite">
            {haceSegundos(cargadoEn, ahora)}
          </span>
        }
      >
        <table className="min-w-full text-left text-sm">
          <caption className="sr-only">Campos extraídos del PDF</caption>
          <thead>
            <tr className="text-xs text-faint">
              <th scope="col" className="px-4 py-2 font-normal">Campo</th>
              <th scope="col" className="px-4 py-2 font-normal">Valor</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-stone-100">
            {filas.map((fila) => (
              <tr key={fila.campo}>
                <td className="px-4 py-3 text-ink">{fila.campo}</td>
                <td className="px-4 py-3">
                  {fila.valor ?? <Etiqueta tono="advertencia">vacío</Etiqueta>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Tarjeta>

      <div className="flex flex-wrap gap-3">
        <Link
          to="/procesamiento"
          className="rounded-md border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-ink hover:bg-stone-50 focus-visible:ring-2 focus-visible:ring-institucional-500"
        >
          Volver a la bandeja
        </Link>
        {puedeConfirmar ? (
          <Link
            to={`/procesamiento/${datos.id}/confirmar`}
            className="rounded-md bg-ink px-4 py-2 text-sm font-medium text-white hover:bg-stone-800 focus-visible:ring-2 focus-visible:ring-institucional-500"
          >
            Validar datos (operador)
          </Link>
        ) : null}
      </div>
    </div>
  );
}
