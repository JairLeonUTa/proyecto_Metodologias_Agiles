import { useState } from 'react';
import type { FormEvent } from 'react';
import { carrerasApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import {
  Alerta,
  Boton,
  Campo,
  Casilla,
  EntradaTexto,
  Etiqueta,
  Modal,
  Tabla,
  Tarjeta,
} from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Carrera } from '../types/catalogo';
import { ROLES } from '../types/comunes';

interface FormularioCarrera {
  codigo: string;
  nombre: string;
  facultad: string;
  activa: boolean;
}

const FORMULARIO_VACIO: FormularioCarrera = { codigo: '', nombre: '', facultad: 'FISEI', activa: true };

/**
 * Listado y registro de carreras del catálogo académico (S1-A04).
 * La escritura está reservada al rol Administrador, tal como exige el Gateway.
 */
export function CarrerasPage(): JSX.Element {
  useTituloPagina('Carreras');

  const { tieneRol } = useAuth();
  const esAdministrador = tieneRol(ROLES.ADMINISTRADOR);

  const carreras = usePeticion(() => carrerasApi.listar(), []);
  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();

  const [modalAbierto, setModalAbierto] = useState(false);
  const [enEdicion, setEnEdicion] = useState<Carrera | null>(null);
  const [formulario, setFormulario] = useState<FormularioCarrera>(FORMULARIO_VACIO);
  const [errores, setErrores] = useState<{ codigo?: string; nombre?: string }>({});
  const [guardando, setGuardando] = useState(false);

  const abrirCreacion = (): void => {
    setEnEdicion(null);
    setFormulario(FORMULARIO_VACIO);
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const abrirEdicion = (carrera: Carrera): void => {
    setEnEdicion(carrera);
    setFormulario({
      codigo: carrera.codigo,
      nombre: carrera.nombre,
      facultad: carrera.facultad,
      activa: carrera.activa,
    });
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const guardar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    limpiar();

    const nuevos: { codigo?: string; nombre?: string } = {};
    if (!enEdicion && !formulario.codigo.trim()) {
      nuevos.codigo = 'El código de la carrera es obligatorio.';
    }
    if (formulario.codigo.trim().length > 20) {
      nuevos.codigo = 'El código admite hasta 20 caracteres.';
    }
    if (!formulario.nombre.trim()) {
      nuevos.nombre = 'El nombre de la carrera es obligatorio.';
    }

    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) {
      return;
    }

    setGuardando(true);
    try {
      if (enEdicion) {
        await carrerasApi.actualizar(enEdicion.id, {
          nombre: formulario.nombre.trim(),
          facultad: formulario.facultad.trim(),
          activa: formulario.activa,
        });
        exito(`Carrera ${formulario.codigo} actualizada correctamente.`);
      } else {
        await carrerasApi.crear({
          codigo: formulario.codigo.trim().toUpperCase(),
          nombre: formulario.nombre.trim(),
          facultad: formulario.facultad.trim() || 'FISEI',
          activa: formulario.activa,
        });
        exito(`Carrera ${formulario.codigo.trim().toUpperCase()} registrada correctamente.`);
      }
      setModalAbierto(false);
      carreras.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setGuardando(false);
    }
  };

  const columnas: Array<ColumnaTabla<Carrera>> = [
    {
      clave: 'codigo',
      encabezado: 'Código',
      render: (carrera) => <span className="font-mono text-xs font-semibold text-slate-800">{carrera.codigo}</span>,
    },
    { clave: 'nombre', encabezado: 'Carrera', render: (carrera) => <span className="font-medium text-slate-800">{carrera.nombre}</span> },
    { clave: 'facultad', encabezado: 'Facultad', render: (carrera) => <span className="text-slate-600">{carrera.facultad}</span> },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (carrera) => <Etiqueta tono={carrera.activa ? 'exito' : 'neutro'}>{carrera.activa ? 'Activa' : 'Inactiva'}</Etiqueta>,
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      render: (carrera) =>
        esAdministrador ? (
          <Boton variante="fantasma" tamano="sm" onClick={() => abrirEdicion(carrera)} aria-label={`Editar la carrera ${carrera.nombre}`}>
            Editar
          </Boton>
        ) : (
          <span className="text-xs text-slate-400">Solo lectura</span>
        ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Catálogo académico"
        titulo="Carreras"
        descripcion="Registro y consulta de las carreras de la Universidad Técnica de Ambato utilizadas en la homologación."
        acciones={
          esAdministrador ? (
            <Boton onClick={abrirCreacion}>Registrar carrera</Boton>
          ) : (
            <Etiqueta tono="informacion">Rol sin permisos de escritura</Etiqueta>
          )
        }
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Carreras registradas"
        descripcion={`${carreras.datos?.length ?? 0} carrera(s) en el catálogo.`}
        compacta
        acciones={<Boton variante="secundario" tamano="sm" onClick={() => carreras.recargar()}>Actualizar</Boton>}
      >
        {carreras.error ? (
          <div className="p-4">
            <Alerta tono="error" titulo="No se pudo cargar el catálogo de carreras">
              {carreras.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={carreras.datos ?? []}
            claveFila={(carrera) => carrera.id}
            cargando={carreras.cargando}
            mensajeVacio="Aún no hay carreras registradas."
          />
        )}
      </Tarjeta>

      <Modal
        abierto={modalAbierto}
        titulo={enEdicion ? `Editar carrera ${enEdicion.codigo}` : 'Registrar carrera'}
        descripcion="Los campos marcados con asterisco son obligatorios."
        onCerrar={() => setModalAbierto(false)}
        acciones={
          <>
            <Boton variante="secundario" onClick={() => setModalAbierto(false)} disabled={guardando}>
              Cancelar
            </Boton>
            <Boton type="submit" form="formulario-carrera" cargando={guardando} textoCargando="Guardando…">
              {enEdicion ? 'Guardar cambios' : 'Registrar'}
            </Boton>
          </>
        }
      >
        <form id="formulario-carrera" onSubmit={(evento) => void guardar(evento)} className="space-y-4" noValidate>
          {mensaje && mensaje.tipo === 'error' ? <Alerta tono="error" detalles={mensaje.detalles}>{mensaje.texto}</Alerta> : null}

          <Campo
            htmlFor="carrera-codigo"
            etiqueta="Código"
            obligatorio
            ayuda="Identificador corto y único (por ejemplo, SW). Se almacena en mayúsculas."
            error={errores.codigo}
          >
            <EntradaTexto
              id="carrera-codigo"
              value={formulario.codigo}
              maxLength={20}
              disabled={Boolean(enEdicion)}
              invalido={Boolean(errores.codigo)}
              onChange={(evento) => setFormulario({ ...formulario, codigo: evento.target.value })}
            />
          </Campo>

          <Campo htmlFor="carrera-nombre" etiqueta="Nombre de la carrera" obligatorio error={errores.nombre}>
            <EntradaTexto
              id="carrera-nombre"
              value={formulario.nombre}
              maxLength={150}
              invalido={Boolean(errores.nombre)}
              onChange={(evento) => setFormulario({ ...formulario, nombre: evento.target.value })}
            />
          </Campo>

          <Campo htmlFor="carrera-facultad" etiqueta="Facultad" ayuda="Valor por defecto: FISEI.">
            <EntradaTexto
              id="carrera-facultad"
              value={formulario.facultad}
              maxLength={60}
              onChange={(evento) => setFormulario({ ...formulario, facultad: evento.target.value })}
            />
          </Campo>

          <Casilla
            id="carrera-activa"
            etiqueta="Carrera activa"
            ayuda="Las carreras inactivas permanecen en el histórico pero no se ofrecen en nuevos trámites."
            checked={formulario.activa}
            onChange={(evento) => setFormulario({ ...formulario, activa: evento.target.checked })}
          />
        </form>
      </Modal>
    </div>
  );
}
