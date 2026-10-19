import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { expedientesApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Campo, EntradaTexto, Etiqueta, Tarjeta } from '../components/ui';
import { useMensajes } from '../hooks/useMensajes';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Expediente } from '../types/expedientes';
import { ETIQUETA_ESTADO, ETIQUETA_TIPO_TRAMITE } from '../types/comunes';
import { esGuid, formatearFechaHora } from '../utils/formato';

/**
 * Consulta del estado de recepción de un expediente por su identificador (S1-A10).
 *
 * Admite el GUID del expediente (`GET /api/expedientes/{id}`) o el código legible
 * (por ejemplo, EXP-20261018-AB12CD), que se busca en el listado del servicio.
 */
export function ConsultaExpedientePage(): JSX.Element {
  useTituloPagina('Consultar estado del expediente');

  const [identificador, setIdentificador] = useState('');
  const [buscando, setBuscando] = useState(false);
  const [resultado, setResultado] = useState<Expediente | null>(null);
  const [buscado, setBuscado] = useState<string | null>(null);
  const { mensaje, error: reportarError, limpiar } = useMensajes();

  const consultar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    limpiar();
    setResultado(null);

    const valor = identificador.trim();
    if (!valor) {
      reportarError('Ingrese el identificador o el código del expediente.');
      return;
    }

    setBuscando(true);
    setBuscado(valor);
    try {
      if (esGuid(valor)) {
        setResultado(await expedientesApi.obtener(valor));
      } else {
        const lista = await expedientesApi.listar();
        const encontrado = lista.find((item) => item.codigo.toUpperCase() === valor.toUpperCase());
        if (!encontrado) {
          reportarError(
            `No se encontró ningún expediente con el código ${valor}. Verifique el código o consulte la bandeja de expedientes.`,
          );
          return;
        }
        setResultado(encontrado);
      }
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setBuscando(false);
    }
  };

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Expedientes"
        titulo="Consultar estado de recepción"
        descripcion="Ingrese el identificador GUID o el código del expediente para conocer su estado de recepción, publicación y contenido registrado."
      />

      <Tarjeta titulo="Búsqueda del expediente" descripcion="El identificador GUID aparece en la respuesta del registro (HTTP 202).">
        <form onSubmit={(evento) => void consultar(evento)} className="space-y-4" noValidate>
          <Campo
            htmlFor="identificador-expediente"
            etiqueta="Identificador o código del expediente"
            obligatorio
            ayuda="Ejemplos: 5f0f1c62-3a2e-4b7e-9f8f-0f4a1c2b3d4e o EXP-20261018-AB12CD."
          >
            <EntradaTexto
              id="identificador-expediente"
              value={identificador}
              disabled={buscando}
              placeholder="GUID o código EXP-…"
              onChange={(evento) => setIdentificador(evento.target.value)}
            />
          </Campo>

          <div className="flex flex-wrap gap-2">
            <Boton type="submit" cargando={buscando} textoCargando="Consultando…">
              Consultar estado
            </Boton>
            <Boton
              variante="secundario"
              disabled={buscando}
              onClick={() => {
                setIdentificador('');
                setResultado(null);
                setBuscado(null);
                limpiar();
              }}
            >
              Limpiar
            </Boton>
            <Link
              to="/expedientes"
              className="inline-flex items-center rounded-md px-3 py-2 text-sm font-semibold text-institucional-700 hover:bg-institucional-50"
            >
              Ver bandeja completa
            </Link>
          </div>
        </form>
      </Tarjeta>

      {mensaje ? (
        <Alerta tono="error" titulo="No fue posible completar la consulta" detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      {resultado ? (
        <Tarjeta
          titulo={`Expediente ${resultado.codigo}`}
          descripcion={`Consultado como: ${buscado ?? resultado.codigo}`}
          acciones={
            <Link
              to={`/expedientes/${resultado.id}`}
              className="rounded-md bg-institucional-700 px-3 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
            >
              Abrir detalle completo
            </Link>
          }
        >
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <dt className="text-xs uppercase tracking-wide text-slate-500">Estado de recepción</dt>
              <dd className="mt-1">
                <Etiqueta
                  tono={
                    resultado.estado === 'EVALUADO'
                      ? 'exito'
                      : resultado.estado === 'REGISTRADO_PENDIENTE_PUBLICACION'
                        ? 'error'
                        : 'advertencia'
                  }
                >
                  {ETIQUETA_ESTADO[resultado.estado] ?? resultado.estado}
                </Etiqueta>
              </dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-slate-500">Publicación en el bus</dt>
              <dd className="mt-1 text-sm text-slate-800">
                {resultado.eventoPublicado ? 'Publicado' : 'Pendiente'} · {resultado.intentosPublicacion} intento(s)
              </dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-slate-500">Tipo de trámite</dt>
              <dd className="mt-1 text-sm text-slate-800">
                {ETIQUETA_TIPO_TRAMITE[resultado.tipoTramite] ?? resultado.tipoTramite} · {resultado.rutaNormativa}
              </dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-slate-500">Carrera de origen</dt>
              <dd className="mt-1 text-sm text-slate-800">{resultado.carreraOrigenCodigo}</dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-slate-500">Carrera receptora</dt>
              <dd className="mt-1 text-sm text-slate-800">{resultado.carreraDestinoCodigo}</dd>
            </div>
            <div>
              <dt className="text-xs uppercase tracking-wide text-slate-500">Recibido</dt>
              <dd className="mt-1 text-sm text-slate-800">{formatearFechaHora(resultado.fechaCreacionUtc)}</dd>
            </div>
          </dl>

          {!resultado.eventoPublicado && resultado.ultimoErrorPublicacion ? (
            <Alerta tono="advertencia" className="mt-4" titulo="Último error de publicación">
              {resultado.ultimoErrorPublicacion}
            </Alerta>
          ) : null}

          <p className="mt-4 text-xs text-slate-500">
            {resultado.documentos.length} documento(s) y {resultado.asignaturas.length} materia(s) registradas en el
            expediente.
          </p>
        </Tarjeta>
      ) : null}
    </div>
  );
}
