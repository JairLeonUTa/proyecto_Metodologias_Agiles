import { useRef, useState } from 'react';
import { Boton, Etiqueta } from './ui';
import { PDF } from '../types/comunes';
import { formatearBytes, abreviarHash } from '../utils/formato';
import { calcularSha256, validarArchivoPdf } from '../utils/validaciones';
import type { DocumentoExpediente } from '../types/expedientes';

/** Documento seleccionado, con el archivo original y el estado de recepción del servidor. */
export interface ArchivoAdjunto {
  /** Identificador local, solo para las claves de React. */
  id: string;
  nombreArchivo: string;
  tamanoBytes: number;
  hashSha256: string | null;
  archivo: File;
  estadoCarga?: 'pendiente' | 'cargando' | 'cargado' | 'error';
  errorCarga?: string;
  documentoServidor?: DocumentoExpediente;
}

interface PropsCargador {
  archivos: ArchivoAdjunto[];
  onCambiar: (archivos: ArchivoAdjunto[]) => void;
  deshabilitado?: boolean;
}

/**
 * Zona de carga de documentos PDF (S1-A07).
 *
 * Realiza la validación local de tipo y tamaño antes de aceptar el archivo, calcula
 * el SHA-256 en el navegador y mantiene el listado con la opción de retirar documentos.
 */
export function CargadorArchivosPdf({ archivos, onCambiar, deshabilitado = false }: PropsCargador): JSX.Element {
  const entrada = useRef<HTMLInputElement>(null);
  const [errores, setErrores] = useState<string[]>([]);
  const [resaltado, setResaltado] = useState(false);
  const [procesando, setProcesando] = useState(false);

  const agregar = async (seleccionados: FileList | null): Promise<void> => {
    if (!seleccionados || seleccionados.length === 0) {
      return;
    }

    const nuevos: ArchivoAdjunto[] = [];
    const rechazos: string[] = [];
    let cupo = PDF.MAXIMO_ARCHIVOS - archivos.length;

    setProcesando(true);

    for (const archivo of Array.from(seleccionados)) {
      if (cupo <= 0) {
        rechazos.push(`Solo se admiten ${PDF.MAXIMO_ARCHIVOS} documentos por expediente.`);
        break;
      }

      const duplicado = archivos.some((item) => item.nombreArchivo === archivo.name && item.tamanoBytes === archivo.size);
      if (duplicado) {
        rechazos.push(`El archivo "${archivo.name}" ya fue agregado.`);
        continue;
      }

      const errorLocal = validarArchivoPdf(archivo);
      if (errorLocal) {
        rechazos.push(errorLocal);
        continue;
      }

      nuevos.push({
        id: `${archivo.name}-${archivo.lastModified}-${archivo.size}`,
        nombreArchivo: archivo.name,
        tamanoBytes: archivo.size,
        hashSha256: await calcularSha256(archivo),
        archivo,
      });
      cupo -= 1;
    }

    setProcesando(false);
    setErrores(rechazos);

    if (nuevos.length > 0) {
      onCambiar([...archivos, ...nuevos]);
    }

    if (entrada.current) {
      entrada.current.value = '';
    }
  };

  const retirar = (id: string): void => {
    onCambiar(archivos.filter((archivo) => archivo.id !== id));
  };

  const totalBytes = archivos.reduce((suma, archivo) => suma + archivo.tamanoBytes, 0);

  return (
    <div className="space-y-3">
      <div
        onDragOver={(evento) => {
          evento.preventDefault();
          if (!deshabilitado) {
            setResaltado(true);
          }
        }}
        onDragLeave={() => setResaltado(false)}
        onDrop={(evento) => {
          evento.preventDefault();
          setResaltado(false);
          if (!deshabilitado) {
            void agregar(evento.dataTransfer.files);
          }
        }}
        className={[
          'flex flex-col items-center justify-center gap-2 rounded-md border-2 border-dashed px-4 py-6 text-center transition-colors',
          resaltado ? 'border-institucional-500 bg-institucional-50' : 'border-slate-300 bg-slate-50',
          deshabilitado ? 'opacity-60' : '',
        ]
          .filter(Boolean)
          .join(' ')}
      >
        <p className="text-sm font-medium text-slate-700">Arrastre aquí los planes analíticos en PDF</p>
        <p className="text-xs text-slate-500">
          Solo archivos .pdf · máximo {formatearBytes(PDF.TAMANO_MAXIMO_BYTES)} por documento · hasta {PDF.MAXIMO_ARCHIVOS}{' '}
          documentos
        </p>

        <input
          ref={entrada}
          id="archivos-pdf"
          type="file"
          accept=".pdf,application/pdf"
          multiple
          disabled={deshabilitado || procesando}
          className="sr-only"
          onChange={(evento) => void agregar(evento.target.files)}
        />

        <Boton
          variante="secundario"
          tamano="sm"
          disabled={deshabilitado || procesando || archivos.length >= PDF.MAXIMO_ARCHIVOS}
          cargando={procesando}
          textoCargando="Calculando hash…"
          onClick={() => entrada.current?.click()}
        >
          Seleccionar archivos PDF
        </Boton>
      </div>

      {errores.length > 0 ? (
        <ul role="alert" className="space-y-1 rounded-md border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
          {errores.map((error) => (
            <li key={error}>• {error}</li>
          ))}
        </ul>
      ) : null}

      {archivos.length === 0 ? (
        <p className="text-xs text-slate-500">Aún no se han adjuntado documentos al expediente.</p>
      ) : (
        <div className="overflow-x-auto rounded-md border border-slate-200">
          <table className="min-w-full text-left text-sm">
            <caption className="sr-only">Documentos PDF adjuntos al expediente</caption>
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th scope="col" className="px-3 py-2 font-semibold">
                  Archivo
                </th>
                <th scope="col" className="px-3 py-2 font-semibold">
                  Tamaño
                </th>
                <th scope="col" className="px-3 py-2 font-semibold">
                  SHA-256
                </th>
                <th scope="col" className="px-3 py-2 font-semibold">
                  Estado
                </th>
                <th scope="col" className="px-3 py-2 font-semibold">
                  Acciones
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {archivos.map((archivo) => (
                <tr key={archivo.id}>
                  <td className="px-3 py-2 text-slate-700">
                    {archivo.documentoServidor?.nombreArchivo ?? archivo.nombreArchivo}
                    {archivo.documentoServidor ? (
                      <span className="mt-1 block font-mono text-[11px] text-slate-500">
                        ID servidor: {archivo.documentoServidor.id}
                      </span>
                    ) : null}
                    {archivo.errorCarga ? (
                      <span role="alert" className="mt-1 block text-xs text-rose-700">{archivo.errorCarga}</span>
                    ) : null}
                  </td>
                  <td className="px-3 py-2 text-slate-600">
                    {formatearBytes(archivo.documentoServidor?.tamanoBytes ?? archivo.tamanoBytes)}
                  </td>
                  <td className="px-3 py-2 font-mono text-xs text-slate-500">
                    {(archivo.documentoServidor?.hashSha256 ?? archivo.hashSha256) ? (
                      <span title={archivo.documentoServidor?.hashSha256 ?? archivo.hashSha256 ?? undefined}>
                        {abreviarHash(archivo.documentoServidor?.hashSha256 ?? archivo.hashSha256 ?? '', 8)}
                      </span>
                    ) : (
                      <Etiqueta tono="advertencia">no calculado</Etiqueta>
                    )}
                  </td>
                  <td className="px-3 py-2">
                    {archivo.estadoCarga === 'cargando' ? <Etiqueta tono="marca">Subiendo…</Etiqueta> : null}
                    {archivo.documentoServidor ? <Etiqueta tono="exito">Recibido</Etiqueta> : null}
                    {archivo.estadoCarga === 'error' ? <Etiqueta tono="error">Pendiente de reintento</Etiqueta> : null}
                    {!archivo.estadoCarga && !archivo.documentoServidor ? <Etiqueta tono="advertencia">Pendiente</Etiqueta> : null}
                  </td>
                  <td className="px-3 py-2">
                    <Boton
                      variante="fantasma"
                      tamano="sm"
                      disabled={deshabilitado}
                      onClick={() => retirar(archivo.id)}
                      aria-label={`Retirar el archivo ${archivo.nombreArchivo}`}
                    >
                      Retirar
                    </Boton>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={5} className="bg-slate-50 px-3 py-2 text-xs text-slate-500">
                  {archivos.length} documento(s) · {formatearBytes(totalBytes)} en total
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </div>
  );
}
