import { Link } from 'react-router-dom';
import { expedientesApi, extraccionesApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Etiqueta, Tabla } from '../components/ui';
import type { ColumnaTabla, TonoEtiqueta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import type { SolicitudAnalisis } from '../types/analisis';
import { ROLES } from '../types/comunes';
import type { Expediente } from '../types/expedientes';
import { formatearFechaHora } from '../utils/formato';

interface Fila {
  expediente: Expediente;
  analisis: SolicitudAnalisis | null;
}

/** Solicitudes con certificado SIHA y su estado (análisis del agente y revisión docente). */
async function cargar(): Promise<Fila[]> {
  const expedientes = (await expedientesApi.listar()).filter((e) => e.numeroCertificado);
  return Promise.all(
    expedientes.map(async (expediente) => ({
      expediente,
      analisis: await extraccionesApi.obtenerSolicitud(expediente.id).catch(() => null),
    })),
  );
}

export function estadoSolicitud(analisis: SolicitudAnalisis | null): { texto: string; tono: TonoEtiqueta } {
  if (!analisis || analisis.estado === 'PENDIENTE') return { texto: 'Analizando…', tono: 'marca' };
  if (analisis.estado === 'ERROR') return { texto: 'Análisis con error', tono: 'error' };
  if (analisis.estadoRevision === 'RESUELTA') return { texto: 'Resuelta', tono: 'exito' };
  const respondieron = analisis.revisores.filter((r) => r.respondio).length;
  return { texto: `Revisión docente ${respondieron}/2`, tono: 'advertencia' };
}

/**
 * Lista única de solicitudes. El operador y el administrador ven todas; cada docente ve solo
 * las que tiene asignadas como revisor (filtro del servidor).
 */
export function SolicitudesPage(): JSX.Element {
  useTituloPagina('Solicitudes');
  const { usuario } = useAuth();
  const datos = usePeticion(cargar, []);
  const esDocente = usuario?.rol === ROLES.DOCENTE;

  const columnas: Array<ColumnaTabla<Fila>> = [
    {
      clave: 'solicitud',
      encabezado: 'Solicitud',
      render: ({ expediente }) => (
        <div>
          <p className="text-sm font-medium text-ink">{expediente.estudianteNombre}</p>
          <p className="font-mono text-xs text-faint">
            {expediente.codigo} · {expediente.numeroCertificado}
          </p>
        </div>
      ),
    },
    {
      clave: 'carreras',
      encabezado: 'Carreras',
      render: ({ expediente }) => `${expediente.carreraOrigenCodigo} → ${expediente.carreraDestinoCodigo}`,
    },
    {
      clave: 'materias',
      encabezado: 'Materias',
      className: 'num',
      render: ({ expediente }) => expediente.asignaturas.length,
    },
    {
      clave: 'revisores',
      encabezado: 'Docentes revisores',
      render: ({ expediente }) => (
        <ul className="text-xs text-muted">
          {(expediente.revisores ?? []).map((r) => (
            <li key={r.correo}>{r.nombre}</li>
          ))}
        </ul>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: ({ analisis }) => {
        const estado = estadoSolicitud(analisis);
        return <Etiqueta tono={estado.tono}>{estado.texto}</Etiqueta>;
      },
    },
    { clave: 'fecha', encabezado: 'Registrada', render: ({ expediente }) => formatearFechaHora(expediente.fechaCreacionUtc) },
    {
      clave: 'abrir',
      encabezado: '',
      render: ({ expediente }) => (
        <Link to={`/solicitudes/${expediente.id}`} className="text-sm font-semibold text-institucional-700 hover:underline">
          Abrir
        </Link>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo={esDocente ? 'Mis solicitudes por revisar' : 'Solicitudes'}
        descripcion={
          esDocente
            ? 'Solicitudes en las que usted es uno de los dos docentes revisores. Cada una ya trae el análisis del agente.'
            : 'Cada solicitud se analiza automáticamente al subir el certificado y se asigna a sus dos docentes revisores.'
        }
        acciones={
          !esDocente ? (
            <Link
              to={RUTAS.nuevaSolicitud}
              className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
            >
              Registrar solicitud
            </Link>
          ) : null
        }
      />
      {datos.error ? <Alerta tono="error">{datos.error}</Alerta> : null}
      <Tabla
        columnas={columnas}
        filas={datos.datos ?? []}
        claveFila={(f) => f.expediente.id}
        cargando={datos.cargando}
        mensajeVacio={esDocente ? 'No tiene solicitudes asignadas.' : 'Todavía no hay solicitudes registradas.'}
      />
    </div>
  );
}
