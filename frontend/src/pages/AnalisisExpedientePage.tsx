import { Navigate, useParams } from 'react-router-dom';
import { extraccionesApi } from '../api';
import { Alerta, CargandoBloque } from '../components/ui';
import { usePeticion } from '../hooks/usePeticion';

/**
 * Ruta anterior `/evaluacion/{extraccionId}`: ahora el análisis vive en la solicitud completa.
 * Redirige a `/solicitudes/{expedienteId}`.
 */
export function AnalisisExpedientePage(): JSX.Element {
  const { id = '' } = useParams();
  const extraccion = usePeticion(() => extraccionesApi.obtener(id), [id]);
  if (extraccion.cargando) return <CargandoBloque />;
  if (!extraccion.datos) return <Alerta tono="error">{extraccion.error ?? 'La extracción no existe.'}</Alerta>;
  return <Navigate to={`/solicitudes/${extraccion.datos.expedienteId}`} replace />;
}
