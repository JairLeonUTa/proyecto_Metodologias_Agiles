import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { carrerasApi, detallesDeError, expedientesApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Campo, EntradaTexto, Etiqueta, Selector, Tarjeta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Carrera } from '../types/catalogo';
import type {
  CertificadoLeido,
  MateriaManual,
  OpcionesRegistroManual,
  RechazoCertificado,
  RegistroManual,
  SolicitudCertificado,
} from '../types/expedientes';
import { ListaVerificacion, ResumenCertificado, rechazoDe } from './SolicitudNuevaPage';

const UTA = 'Universidad Técnica de Ambato';
const CAMBIO = 'Cambio de carrera';
const MOVILIDAD = 'Movilidad externa';
const NOTA_MINIMA = 7;

/** Materia en pantalla: la nota se guarda como texto para poder escribir "8,50". */
type FilaMateria = Omit<MateriaManual, 'nota'> & { nota: string };

/** Lo que se edita en pantalla; se convierte a {@link RegistroManual} al enviarlo. */
type Formulario = Omit<RegistroManual, 'materias'> & { materias: FilaMateria[] };

const materiaVacia = (nivel = 1): FilaMateria => ({ nombre: '', nivel, periodoAcademico: '', nota: '', aprobada: true });

/** "8,5" → 8.5; vacío o inválido → null. */
function leerNota(texto: string): number | null {
  if (texto.trim() === '') return null;
  const nota = Number(texto.trim().replace(',', '.'));
  return Number.isNaN(nota) ? null : nota;
}

const aEnviar = (f: Formulario): RegistroManual => ({ ...f, materias: f.materias.map((m) => ({ ...m, nota: leerNota(m.nota) })) });

const registroInicial = (): Formulario => ({
  estudianteNombre: '',
  cedula: '',
  tipoTramite: CAMBIO,
  institucionOrigen: UTA,
  carreraOrigen: '',
  carreraDestino: '',
  revisor1Correo: '',
  revisor2Correo: '',
  materias: [materiaVacia()],
  solicitudes: [{ materiasOrigen: [''], materiaDestino: '' }],
});

/** Nombre de la malla ("ALGORITMOS Y LOGICA") en un formato más fácil de leer ("Algoritmos y logica"). */
function legible(nombre: string): string {
  const minuscula = nombre.toLocaleLowerCase('es');
  return minuscula.charAt(0).toLocaleUpperCase('es') + minuscula.slice(1);
}

/**
 * Registro manual de una solicitud de homologación, para cuando no se tiene el certificado SIHA en PDF.
 * Pide los mismos datos del certificado y los revisa con exactamente las mismas reglas; después la
 * solicitud sigue igual: el agente la analiza y la reciben los dos docentes revisores.
 */
export function RegistroManualPage(): JSX.Element {
  useTituloPagina('Registro manual');
  const { usuario } = useAuth();
  const [registro, setRegistro] = useState<Formulario>(registroInicial);
  const [carreras, setCarreras] = useState<Carrera[]>([]);
  const [opciones, setOpciones] = useState<OpcionesRegistroManual>({ docentes: [], materiasOrigen: [], materiasDestino: [] });
  const [revisando, setRevisando] = useState(false);
  const [registrando, setRegistrando] = useState(false);
  const [revisado, setRevisado] = useState<{ datos: string; certificado: CertificadoLeido } | null>(null);
  const [rechazo, setRechazo] = useState<RechazoCertificado | null>(null);
  const [error, setError] = useState<{ texto: string; detalles: string[] } | null>(null);
  const [resultado, setResultado] = useState<SolicitudCertificado | null>(null);

  const esMovilidad = registro.tipoTramite === MOVILIDAD;
  const datosActuales = JSON.stringify(registro);
  const listoParaRegistrar = revisado !== null && revisado.datos === datosActuales;

  useEffect(() => {
    carrerasApi
      .listar()
      .then((lista) => setCarreras(lista.filter((c) => c.activa)))
      .catch(() => setCarreras([]));
  }, []);

  useEffect(() => {
    let vigente = true;
    expedientesApi
      .opcionesManual(esMovilidad ? '' : registro.carreraOrigen, registro.carreraDestino)
      .then((o) => vigente && setOpciones(o))
      .catch((fallo) => vigente && setError({ texto: mensajeDeError(fallo), detalles: [] }));
    return () => {
      vigente = false;
    };
  }, [esMovilidad, registro.carreraOrigen, registro.carreraDestino]);

  const aprobadas = useMemo(
    () => registro.materias.filter((m) => m.aprobada && m.nombre.trim()).map((m) => m.nombre.trim()),
    [registro.materias],
  );

  const cambiar = (parcial: Partial<Formulario>): void => {
    setRegistro((r) => ({ ...r, ...parcial }));
    setRechazo(null);
  };

  const cambiarMateria = (i: number, parcial: Partial<FilaMateria>): void =>
    cambiar({ materias: registro.materias.map((m, j) => (j === i ? { ...m, ...parcial } : m)) });

  const cambiarNota = (i: number, texto: string): void => {
    const nota = leerNota(texto);
    cambiarMateria(i, nota === null ? { nota: texto } : { nota: texto, aprobada: nota >= NOTA_MINIMA });
  };

  const cambiarSolicitud = (i: number, parcial: Partial<Formulario['solicitudes'][number]>): void =>
    cambiar({ solicitudes: registro.solicitudes.map((s, j) => (j === i ? { ...s, ...parcial } : s)) });

  const revisar = async (): Promise<void> => {
    setRevisando(true);
    setError(null);
    setRechazo(null);
    setRevisado(null);
    try {
      const certificado = await expedientesApi.validarManual(aEnviar(registro));
      setRevisado({ datos: datosActuales, certificado });
    } catch (fallo) {
      const conLista = rechazoDe(fallo);
      if (conLista) setRechazo(conLista);
      else setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setRevisando(false);
    }
  };

  const registrar = async (): Promise<void> => {
    setRegistrando(true);
    setError(null);
    try {
      setResultado(await expedientesApi.registrarManual(aEnviar(registro)));
    } catch (fallo) {
      const conLista = rechazoDe(fallo);
      if (conLista) setRechazo(conLista);
      else setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setRegistrando(false);
    }
  };

  const otraSolicitud = (): void => {
    setRegistro(registroInicial());
    setRevisado(null);
    setRechazo(null);
    setError(null);
    setResultado(null);
  };

  if (resultado) {
    return (
      <div className="space-y-5">
        <EncabezadoPagina titulo="Registro manual de solicitud" />
        <Tarjeta titulo={resultado.publicado ? 'Solicitud registrada y enviada' : 'Solicitud registrada — envío pendiente'} descripcion={resultado.mensaje}>
          <div className="space-y-4">
            <Alerta tono={resultado.publicado ? 'exito' : 'advertencia'} titulo={`Expediente ${resultado.expediente.codigo}`}>
              {resultado.certificado.estudianteNombre} · {resultado.certificado.carreraOrigen} → {resultado.certificado.carreraDestino} ·{' '}
              {resultado.certificado.solicitudes.length} materia(s) pedida(s). Asignada a{' '}
              {resultado.certificado.revisores.map((r) => r.nombre).join(' y ')}. El agente ya está analizando los contenidos.
            </Alerta>
            <div className="flex flex-wrap gap-2">
              <Link
                to={`/solicitudes/${resultado.expediente.id}`}
                className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
              >
                Ver la solicitud y su análisis
              </Link>
              <Boton variante="secundario" onClick={otraSolicitud}>
                Registrar otra solicitud a mano
              </Boton>
            </div>
          </div>
        </Tarjeta>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <EncabezadoPagina
        titulo="Registro manual de solicitud"
        descripcion="Para cuando no tiene el certificado SIHA en PDF. Escriba los mismos datos del certificado: se revisan con las mismas reglas y, al registrarla, el agente la analiza y la reciben los dos docentes revisores."
        acciones={usuario ? <Etiqueta tono="marca">Operador: {usuario.nombreCompleto}</Etiqueta> : null}
      />

      <datalist id="materias-origen">
        {opciones.materiasOrigen.map((m) => (
          <option key={m} value={legible(m)} />
        ))}
      </datalist>
      <datalist id="materias-destino">
        {opciones.materiasDestino.map((m) => (
          <option key={m} value={legible(m)} />
        ))}
      </datalist>

      <Tarjeta titulo="1. Estudiante">
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo htmlFor="estudiante" etiqueta="Nombres y apellidos completos" obligatorio>
            <EntradaTexto
              id="estudiante"
              value={registro.estudianteNombre}
              onChange={(e) => cambiar({ estudianteNombre: e.target.value })}
              placeholder="Ej. Pérez Gómez Ana Lucía"
            />
          </Campo>
          <Campo htmlFor="cedula" etiqueta="Cédula" obligatorio ayuda="10 dígitos, sin guion.">
            <EntradaTexto
              id="cedula"
              inputMode="numeric"
              maxLength={10}
              value={registro.cedula}
              onChange={(e) => cambiar({ cedula: e.target.value.replace(/\D/g, '') })}
              placeholder="1804567890"
            />
          </Campo>
        </div>
      </Tarjeta>

      <Tarjeta titulo="2. Trámite">
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo htmlFor="tipo" etiqueta="Tipo de trámite" obligatorio>
            <Selector
              id="tipo"
              value={registro.tipoTramite}
              onChange={(e) =>
                cambiar({
                  tipoTramite: e.target.value,
                  institucionOrigen: e.target.value === CAMBIO ? UTA : '',
                  carreraOrigen: '',
                })
              }
            >
              <option value={CAMBIO}>Cambio de carrera (dentro de la UTA)</option>
              <option value={MOVILIDAD}>Movilidad externa (viene de otra universidad)</option>
            </Selector>
          </Campo>
          <Campo htmlFor="institucion" etiqueta="Institución de origen" obligatorio>
            <EntradaTexto
              id="institucion"
              value={registro.institucionOrigen}
              disabled={!esMovilidad}
              onChange={(e) => cambiar({ institucionOrigen: e.target.value })}
              placeholder="Nombre completo de la universidad"
            />
          </Campo>
          <Campo htmlFor="carrera-origen" etiqueta="Carrera de origen" obligatorio ayuda={esMovilidad ? 'Nombre completo, sin siglas.' : undefined}>
            {esMovilidad ? (
              <EntradaTexto
                id="carrera-origen"
                value={registro.carreraOrigen}
                onChange={(e) => cambiar({ carreraOrigen: e.target.value })}
                placeholder="Ej. Ingeniería en Computación"
              />
            ) : (
              <Selector id="carrera-origen" value={registro.carreraOrigen} onChange={(e) => cambiar({ carreraOrigen: e.target.value })}>
                <option value="">Elija la carrera…</option>
                {carreras.map((c) => (
                  <option key={c.id} value={c.nombre}>
                    {c.nombre}
                  </option>
                ))}
              </Selector>
            )}
          </Campo>
          <Campo htmlFor="carrera-destino" etiqueta="Carrera a la que quiere pasar" obligatorio>
            <Selector id="carrera-destino" value={registro.carreraDestino} onChange={(e) => cambiar({ carreraDestino: e.target.value })}>
              <option value="">Elija la carrera…</option>
              {carreras.map((c) => (
                <option key={c.id} value={c.nombre}>
                  {c.nombre}
                </option>
              ))}
            </Selector>
          </Campo>
        </div>
      </Tarjeta>

      <Tarjeta titulo="3. Docentes revisores" descripcion="Los dos docentes que van a decidir esta solicitud.">
        <div className="grid gap-4 sm:grid-cols-2">
          {(['revisor1Correo', 'revisor2Correo'] as const).map((campo, i) => (
            <Campo key={campo} htmlFor={campo} etiqueta={`Docente revisor ${i + 1}`} obligatorio>
              <Selector id={campo} value={registro[campo]} onChange={(e) => cambiar({ [campo]: e.target.value })}>
                <option value="">Elija un docente…</option>
                {opciones.docentes.map((d) => (
                  <option key={d.correo} value={d.correo}>
                    {d.nombre} ({d.correo})
                  </option>
                ))}
              </Selector>
            </Campo>
          ))}
        </div>
      </Tarjeta>

      <Tarjeta
        titulo="4. Historial académico"
        descripcion="Todas las materias que cursó en la carrera de origen, aprobadas y reprobadas. Al escribir la nota se marca sola como Aprobada (7 o más) o Reprobada."
      >
        <div className="space-y-3">
          {registro.materias.map((m, i) => (
            <fieldset key={i} className="grid items-end gap-3 rounded-md border border-slate-200 p-3 sm:grid-cols-12">
              <legend className="sr-only">Materia {i + 1}</legend>
              <Campo htmlFor={`materia-${i}`} etiqueta="Materia" className="sm:col-span-4">
                <EntradaTexto
                  id={`materia-${i}`}
                  list="materias-origen"
                  value={m.nombre}
                  onChange={(e) => cambiarMateria(i, { nombre: e.target.value })}
                  placeholder="Nombre completo de la materia"
                />
              </Campo>
              <Campo htmlFor={`nivel-${i}`} etiqueta="Nivel" className="sm:col-span-1">
                <EntradaTexto
                  id={`nivel-${i}`}
                  type="number"
                  min={1}
                  max={10}
                  value={m.nivel ?? ''}
                  onChange={(e) => cambiarMateria(i, { nivel: e.target.value === '' ? null : Number(e.target.value) })}
                />
              </Campo>
              <Campo htmlFor={`periodo-${i}`} etiqueta="Período académico" className="sm:col-span-3">
                <EntradaTexto
                  id={`periodo-${i}`}
                  value={m.periodoAcademico}
                  onChange={(e) => cambiarMateria(i, { periodoAcademico: e.target.value })}
                  placeholder="Ej. Abr-Sep 2025"
                />
              </Campo>
              <Campo htmlFor={`nota-${i}`} etiqueta="Nota" className="sm:col-span-1">
                <EntradaTexto
                  id={`nota-${i}`}
                  inputMode="decimal"
                  value={m.nota}
                  onChange={(e) => cambiarNota(i, e.target.value)}
                  placeholder="8,50"
                />
              </Campo>
              <Campo htmlFor={`estado-${i}`} etiqueta="Estado" className="sm:col-span-2">
                <Selector
                  id={`estado-${i}`}
                  value={m.aprobada ? 'si' : 'no'}
                  onChange={(e) => cambiarMateria(i, { aprobada: e.target.value === 'si' })}
                >
                  <option value="si">Aprobada</option>
                  <option value="no">Reprobada</option>
                </Selector>
              </Campo>
              <div className="sm:col-span-1">
                <Boton
                  variante="fantasma"
                  tamano="sm"
                  disabled={registro.materias.length === 1}
                  onClick={() => cambiar({ materias: registro.materias.filter((_, j) => j !== i) })}
                  aria-label={`Quitar la materia ${i + 1}`}
                >
                  Quitar
                </Boton>
              </div>
            </fieldset>
          ))}
          <Boton
            variante="secundario"
            onClick={() => cambiar({ materias: [...registro.materias, materiaVacia(registro.materias.at(-1)?.nivel ?? 1)] })}
          >
            + Agregar otra materia
          </Boton>
        </div>
      </Tarjeta>

      <Tarjeta
        titulo="5. Materias que pide homologar"
        descripcion="Por cada materia de la carrera nueva, elija la materia aprobada con la que la quiere homologar. Si son dos materias aprobadas para una sola, agregue la segunda."
      >
        <div className="space-y-3">
          {registro.solicitudes.map((s, i) => (
            <fieldset key={i} className="space-y-3 rounded-md border border-slate-200 p-3">
              <legend className="px-1 text-sm font-semibold text-ink">Solicitud {i + 1}</legend>
              <div className="grid items-end gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  {s.materiasOrigen.map((o, k) => (
                    <Campo key={k} htmlFor={`origen-${i}-${k}`} etiqueta={k === 0 ? 'Materia aprobada' : 'Otra materia aprobada'}>
                      <Selector
                        id={`origen-${i}-${k}`}
                        value={o}
                        onChange={(e) =>
                          cambiarSolicitud(i, { materiasOrigen: s.materiasOrigen.map((x, y) => (y === k ? e.target.value : x)) })
                        }
                      >
                        <option value="">Elija una materia aprobada…</option>
                        {aprobadas.map((a) => (
                          <option key={a} value={a}>
                            {a}
                          </option>
                        ))}
                      </Selector>
                    </Campo>
                  ))}
                  <Boton variante="fantasma" tamano="sm" onClick={() => cambiarSolicitud(i, { materiasOrigen: [...s.materiasOrigen, ''] })}>
                    + Otra materia aprobada
                  </Boton>
                </div>
                <Campo htmlFor={`destino-${i}`} etiqueta="Homologar por (materia de la carrera nueva)">
                  <EntradaTexto
                    id={`destino-${i}`}
                    list="materias-destino"
                    value={s.materiaDestino}
                    onChange={(e) => cambiarSolicitud(i, { materiaDestino: e.target.value })}
                    placeholder="Escriba o elija de la lista"
                  />
                </Campo>
              </div>
              <Boton
                variante="fantasma"
                tamano="sm"
                disabled={registro.solicitudes.length === 1}
                onClick={() => cambiar({ solicitudes: registro.solicitudes.filter((_, j) => j !== i) })}
              >
                Quitar esta solicitud
              </Boton>
            </fieldset>
          ))}
          <Boton
            variante="secundario"
            onClick={() => cambiar({ solicitudes: [...registro.solicitudes, { materiasOrigen: [''], materiaDestino: '' }] })}
          >
            + Agregar otra materia a homologar
          </Boton>
        </div>
      </Tarjeta>

      {rechazo ? (
        <Tarjeta titulo="Hay datos por corregir" descripcion="Corrija lo marcado con ✘ y vuelva a pulsar «Revisar los datos».">
          <ListaVerificacion secciones={rechazo.secciones} />
        </Tarjeta>
      ) : null}

      {error ? (
        <Alerta tono="error" detalles={error.detalles}>
          {error.texto}
        </Alerta>
      ) : null}

      {listoParaRegistrar && revisado ? (
        <Tarjeta titulo="Datos revisados: todo está correcto" descripcion="Confirme que coinciden con los documentos del estudiante.">
          <div className="space-y-4">
            {revisado.certificado.avisos?.length ? (
              <Alerta tono="advertencia" titulo="Revise el nombre de estas materias">
                <ul className="list-disc pl-4">
                  {revisado.certificado.avisos.map((a) => (
                    <li key={a.materiaDestino}>{a.mensaje}</li>
                  ))}
                </ul>
              </Alerta>
            ) : null}
            <ResumenCertificado certificado={revisado.certificado} />
          </div>
        </Tarjeta>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Boton variante="secundario" onClick={() => void revisar()} cargando={revisando} textoCargando="Revisando…">
          Revisar los datos
        </Boton>
        <Boton onClick={() => void registrar()} disabled={!listoParaRegistrar} cargando={registrando} textoCargando="Registrando…">
          Registrar y enviar solicitud
        </Boton>
        {!listoParaRegistrar ? (
          <span className="text-sm text-muted">Primero pulse «Revisar los datos»; si todo está bien, se habilita el registro.</span>
        ) : null}
      </div>
    </div>
  );
}
