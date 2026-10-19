import { useState } from 'react';
import type { FormEvent } from 'react';
import { mensajeDeError, rolesApi, usuariosApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import {
  Alerta,
  Boton,
  Campo,
  Casilla,
  EntradaTexto,
  Etiqueta,
  Modal,
  Selector,
  Tabla,
  Tarjeta,
} from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Usuario } from '../types/seguridad';
import { ETIQUETA_ROL } from '../types/comunes';
import { formatearFechaHora } from '../utils/formato';
import { esCorreoInstitucional } from '../utils/validaciones';

interface FormularioUsuario {
  correoInstitucional: string;
  nombreCompleto: string;
  password: string;
  rol: string;
  activo: boolean;
}

const FORMULARIO_VACIO: FormularioUsuario = {
  correoInstitucional: '',
  nombreCompleto: '',
  password: '',
  rol: 'OPERADOR',
  activo: true,
};

/**
 * Gestión de cuentas institucionales y asignación de roles (S1-A02, RBAC).
 * Replica las restricciones del Gateway: solo el rol Administrador accede a esta vista.
 */
export function UsuariosPage(): JSX.Element {
  useTituloPagina('Usuarios y roles');

  const [filtroActivo, setFiltroActivo] = useState('');
  const usuarios = usePeticion(
    () => usuariosApi.listar(filtroActivo === '' ? undefined : filtroActivo === 'true'),
    [filtroActivo],
  );
  const roles = usePeticion(() => rolesApi.listar(), []);

  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();
  const [modalAbierto, setModalAbierto] = useState(false);
  const [enEdicion, setEnEdicion] = useState<Usuario | null>(null);
  const [formulario, setFormulario] = useState<FormularioUsuario>(FORMULARIO_VACIO);
  const [errores, setErrores] = useState<Record<string, string>>({});
  const [guardando, setGuardando] = useState(false);
  const [porDesactivar, setPorDesactivar] = useState<Usuario | null>(null);
  const [procesandoBaja, setProcesandoBaja] = useState(false);

  const abrirCreacion = (): void => {
    setEnEdicion(null);
    setFormulario({ ...FORMULARIO_VACIO, rol: roles.datos?.[0]?.nombre ?? 'OPERADOR' });
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const abrirEdicion = (usuario: Usuario): void => {
    setEnEdicion(usuario);
    setFormulario({
      correoInstitucional: usuario.correoInstitucional,
      nombreCompleto: usuario.nombreCompleto,
      password: '',
      rol: usuario.rol,
      activo: usuario.activo,
    });
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const guardar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    limpiar();

    const nuevos: Record<string, string> = {};
    if (!formulario.nombreCompleto.trim() || formulario.nombreCompleto.trim().length < 3) {
      nuevos.nombreCompleto = 'El nombre completo debe tener al menos 3 caracteres.';
    }
    if (!formulario.rol) {
      nuevos.rol = 'Seleccione el rol de la cuenta.';
    }
    if (!enEdicion) {
      if (!esCorreoInstitucional(formulario.correoInstitucional)) {
        nuevos.correoInstitucional = 'Ingrese un correo institucional válido.';
      }
      if (formulario.password.length < 8) {
        nuevos.password = 'La contraseña debe tener al menos 8 caracteres.';
      }
    }

    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) {
      return;
    }

    setGuardando(true);
    try {
      if (enEdicion) {
        await usuariosApi.actualizar(enEdicion.id, {
          nombreCompleto: formulario.nombreCompleto.trim(),
          rol: formulario.rol,
          activo: formulario.activo,
        });
        exito(`Cuenta ${enEdicion.correoInstitucional} actualizada correctamente.`);
      } else {
        await usuariosApi.crear({
          correoInstitucional: formulario.correoInstitucional.trim().toLowerCase(),
          nombreCompleto: formulario.nombreCompleto.trim(),
          password: formulario.password,
          rol: formulario.rol,
          activo: formulario.activo,
        });
        exito(`Cuenta ${formulario.correoInstitucional.trim().toLowerCase()} creada correctamente.`);
      }
      setModalAbierto(false);
      usuarios.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setGuardando(false);
    }
  };

  const alternarEstado = async (usuario: Usuario): Promise<void> => {
    limpiar();
    try {
      await usuariosApi.cambiarEstado(usuario.id, !usuario.activo);
      exito(`La cuenta ${usuario.correoInstitucional} quedó ${usuario.activo ? 'inactiva' : 'activa'}.`);
      usuarios.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    }
  };

  const confirmarBaja = async (): Promise<void> => {
    if (!porDesactivar) {
      return;
    }
    limpiar();
    setProcesandoBaja(true);
    try {
      await usuariosApi.desactivar(porDesactivar.id);
      exito(`Cuenta ${porDesactivar.correoInstitucional} dada de baja (baja lógica).`);
      setPorDesactivar(null);
      usuarios.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setProcesandoBaja(false);
    }
  };

  const columnas: Array<ColumnaTabla<Usuario>> = [
    {
      clave: 'nombre',
      encabezado: 'Usuario',
      render: (usuario) => (
        <div>
          <p className="font-medium text-slate-800">{usuario.nombreCompleto}</p>
          <p className="text-xs text-slate-500">{usuario.correoInstitucional}</p>
        </div>
      ),
    },
    {
      clave: 'rol',
      encabezado: 'Rol',
      render: (usuario) => (
        <Etiqueta tono={usuario.rol === 'ADMINISTRADOR' ? 'marca' : usuario.rol === 'DOCENTE' ? 'informacion' : 'neutro'}>
          {ETIQUETA_ROL[usuario.rol] ?? usuario.rol}
        </Etiqueta>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (usuario) => (
        <Etiqueta tono={usuario.activo ? 'exito' : 'error'}>{usuario.activo ? 'Activa' : 'Inactiva'}</Etiqueta>
      ),
    },
    {
      clave: 'acceso',
      encabezado: 'Último acceso',
      render: (usuario) => (
        <div className="text-xs text-slate-600">
          <p>{formatearFechaHora(usuario.ultimoAccesoUtc)}</p>
          <p className="text-slate-400">Alta: {formatearFechaHora(usuario.fechaCreacionUtc)}</p>
        </div>
      ),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      render: (usuario) => (
        <div className="flex flex-wrap items-center gap-1">
          <Boton variante="fantasma" tamano="sm" onClick={() => abrirEdicion(usuario)} aria-label={`Editar la cuenta ${usuario.correoInstitucional}`}>
            Editar
          </Boton>
          <Boton variante="fantasma" tamano="sm" onClick={() => void alternarEstado(usuario)}>
            {usuario.activo ? 'Desactivar' : 'Activar'}
          </Boton>
          {usuario.activo ? (
            <Boton
              variante="fantasma"
              tamano="sm"
              className="text-rose-600 hover:bg-rose-50"
              onClick={() => setPorDesactivar(usuario)}
            >
              Dar de baja
            </Boton>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Administración"
        titulo="Usuarios y roles"
        descripcion="Cuentas institucionales del sistema y rol asignado para el control de acceso (RBAC)."
        acciones={<Boton onClick={abrirCreacion}>Crear cuenta</Boton>}
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Cuentas registradas"
        descripcion={`${usuarios.datos?.length ?? 0} cuenta(s).`}
        compacta
        acciones={
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-44">
              <Campo htmlFor="filtro-activo" etiqueta="Estado de la cuenta">
                <Selector id="filtro-activo" value={filtroActivo} onChange={(evento) => setFiltroActivo(evento.target.value)}>
                  <option value="">Todas</option>
                  <option value="true">Activas</option>
                  <option value="false">Inactivas</option>
                </Selector>
              </Campo>
            </div>
            <Boton variante="secundario" tamano="sm" onClick={() => usuarios.recargar()}>
              Actualizar
            </Boton>
          </div>
        }
      >
        {usuarios.error ? (
          <div className="p-4">
            <Alerta tono="error" titulo="No se pudo cargar el listado de usuarios">
              {usuarios.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={usuarios.datos ?? []}
            claveFila={(usuario) => usuario.id}
            cargando={usuarios.cargando}
            mensajeVacio="No hay cuentas registradas."
            pie={`Roles disponibles: ${(roles.datos ?? []).map((rol) => rol.nombre).join(', ') || 'consultando…'}`}
          />
        )}
      </Tarjeta>

      <Modal
        abierto={modalAbierto}
        titulo={enEdicion ? `Editar cuenta ${enEdicion.correoInstitucional}` : 'Crear cuenta institucional'}
        descripcion="El correo institucional no puede modificarse una vez creada la cuenta."
        onCerrar={() => setModalAbierto(false)}
        acciones={
          <>
            <Boton variante="secundario" onClick={() => setModalAbierto(false)} disabled={guardando}>
              Cancelar
            </Boton>
            <Boton type="submit" form="formulario-usuario" cargando={guardando} textoCargando="Guardando…">
              {enEdicion ? 'Guardar cambios' : 'Crear cuenta'}
            </Boton>
          </>
        }
      >
        <form id="formulario-usuario" onSubmit={(evento) => void guardar(evento)} className="space-y-4" noValidate>
          {mensaje && mensaje.tipo === 'error' ? <Alerta tono="error" detalles={mensaje.detalles}>{mensaje.texto}</Alerta> : null}

          <Campo htmlFor="usuario-correo" etiqueta="Correo institucional" obligatorio error={errores.correoInstitucional}>
            <EntradaTexto
              id="usuario-correo"
              type="email"
              value={formulario.correoInstitucional}
              maxLength={150}
              disabled={Boolean(enEdicion)}
              invalido={Boolean(errores.correoInstitucional)}
              placeholder="usuario@uta.edu.ec"
              onChange={(evento) => setFormulario({ ...formulario, correoInstitucional: evento.target.value })}
            />
          </Campo>

          <Campo htmlFor="usuario-nombre" etiqueta="Nombre completo" obligatorio error={errores.nombreCompleto}>
            <EntradaTexto
              id="usuario-nombre"
              value={formulario.nombreCompleto}
              maxLength={150}
              invalido={Boolean(errores.nombreCompleto)}
              onChange={(evento) => setFormulario({ ...formulario, nombreCompleto: evento.target.value })}
            />
          </Campo>

          {!enEdicion ? (
            <Campo
              htmlFor="usuario-password"
              etiqueta="Contraseña inicial"
              obligatorio
              ayuda="Mínimo 8 caracteres. Se almacena como hash BCrypt en el servidor."
              error={errores.password}
            >
              <EntradaTexto
                id="usuario-password"
                type="password"
                value={formulario.password}
                autoComplete="new-password"
                invalido={Boolean(errores.password)}
                onChange={(evento) => setFormulario({ ...formulario, password: evento.target.value })}
              />
            </Campo>
          ) : null}

          <Campo
            htmlFor="usuario-rol"
            etiqueta="Rol"
            obligatorio
            ayuda={(roles.datos ?? []).find((rol) => rol.nombre === formulario.rol)?.descripcion}
            error={errores.rol}
          >
            <Selector
              id="usuario-rol"
              value={formulario.rol}
              invalido={Boolean(errores.rol)}
              onChange={(evento) => setFormulario({ ...formulario, rol: evento.target.value })}
            >
              {(roles.datos ?? []).map((rol) => (
                <option key={rol.id} value={rol.nombre}>
                  {ETIQUETA_ROL[rol.nombre] ?? rol.nombre}
                </option>
              ))}
            </Selector>
          </Campo>

          <Casilla
            id="usuario-activo"
            etiqueta="Cuenta activa"
            ayuda="Una cuenta inactiva no puede iniciar sesión, pero conserva su trazabilidad."
            checked={formulario.activo}
            onChange={(evento) => setFormulario({ ...formulario, activo: evento.target.checked })}
          />
        </form>
      </Modal>

      <Modal
        abierto={porDesactivar !== null}
        titulo="Confirmar baja lógica de la cuenta"
        ancho="md"
        onCerrar={() => setPorDesactivar(null)}
        acciones={
          <>
            <Boton variante="secundario" onClick={() => setPorDesactivar(null)} disabled={procesandoBaja}>
              Cancelar
            </Boton>
            <Boton variante="peligro" cargando={procesandoBaja} textoCargando="Procesando…" onClick={() => void confirmarBaja()}>
              Dar de baja
            </Boton>
          </>
        }
      >
        <Alerta tono="advertencia" titulo="La cuenta dejará de estar activa">
          Se conservará el histórico de expedientes asociados a {porDesactivar?.correoInstitucional}. Esta acción puede
          revertirse activando nuevamente la cuenta.
        </Alerta>
      </Modal>
    </div>
  );
}
