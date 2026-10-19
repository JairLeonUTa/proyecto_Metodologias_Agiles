import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { mensajeDeError } from '../api';
import { Alerta, Boton, Campo, EntradaTexto } from '../components/ui';
import { LayoutAutenticacion } from '../layouts/LayoutAutenticacion';
import { RUTAS } from '../layouts/navegacion';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { esCorreoInstitucional } from '../utils/validaciones';

interface EstadoUbicacion {
  desde?: string;
}

/**
 * Inicio de sesión institucional (S1-A02 / S1-A08).
 * Consume `POST /api/auth/login` a través del API Gateway, guarda el JWT y el
 * usuario, y redirige a la ruta solicitada originalmente.
 */
export function LoginPage(): JSX.Element {
  useTituloPagina('Inicio de sesión');

  const { iniciarSesion, motivoCierre, limpiarMotivoCierre } = useAuth();
  const navegar = useNavigate();
  const ubicacion = useLocation();
  const estado = (ubicacion.state ?? null) as EstadoUbicacion | null;

  const [correo, setCorreo] = useState('');
  const [password, setPassword] = useState('');
  const [errores, setErrores] = useState<{ correo?: string; password?: string }>({});
  const [errorServidor, setErrorServidor] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const entradaCorreo = useRef<HTMLInputElement>(null);

  // El foco inicial permite operar el formulario solo con el teclado (S1-A10).
  useEffect(() => {
    entradaCorreo.current?.focus();
  }, []);

  const enviar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    setErrorServidor(null);

    const nuevosErrores: { correo?: string; password?: string } = {};
    if (!correo.trim()) {
      nuevosErrores.correo = 'El correo institucional es obligatorio.';
    } else if (!esCorreoInstitucional(correo)) {
      nuevosErrores.correo = 'Ingrese un correo institucional válido (por ejemplo, admin@uta.edu.ec).';
    }
    if (!password) {
      nuevosErrores.password = 'La contraseña es obligatoria.';
    } else if (password.length < 8) {
      nuevosErrores.password = 'La contraseña debe tener al menos 8 caracteres.';
    }

    setErrores(nuevosErrores);
    if (Object.keys(nuevosErrores).length > 0) {
      return;
    }

    setEnviando(true);
    try {
      limpiarMotivoCierre();
      await iniciarSesion(correo, password);
      navegar(estado?.desde ?? RUTAS.inicio, { replace: true });
    } catch (fallo) {
      setErrorServidor(mensajeDeError(fallo));
    } finally {
      setEnviando(false);
    }
  };

  return (
    <LayoutAutenticacion
      titulo="Inicio de sesión institucional"
      subtitulo="Acceda con sus credenciales de la Universidad Técnica de Ambato."
    >
      <div className="space-y-4">
        {motivoCierre ? <Alerta tono="advertencia">{motivoCierre}</Alerta> : null}
        {errorServidor ? <Alerta tono="error" titulo="No fue posible iniciar sesión">{errorServidor}</Alerta> : null}

        <form onSubmit={(evento) => void enviar(evento)} className="space-y-4" noValidate>
          <Campo htmlFor="correo" etiqueta="Correo institucional" obligatorio error={errores.correo}>
            <EntradaTexto
              id="correo"
              ref={entradaCorreo}
              type="email"
              inputMode="email"
              autoComplete="username"
              placeholder="usuario@uta.edu.ec"
              value={correo}
              invalido={Boolean(errores.correo)}
              aria-invalid={Boolean(errores.correo)}
              aria-describedby={errores.correo ? 'correo-error' : undefined}
              onChange={(evento) => setCorreo(evento.target.value)}
            />
          </Campo>

          <Campo
            htmlFor="password"
            etiqueta="Contraseña"
            obligatorio
            ayuda="Mínimo 8 caracteres."
            error={errores.password}
          >
            <EntradaTexto
              id="password"
              type="password"
              autoComplete="current-password"
              placeholder="••••••••"
              value={password}
              invalido={Boolean(errores.password)}
              aria-invalid={Boolean(errores.password)}
              aria-describedby={errores.password ? 'password-error' : 'password-ayuda'}
              onChange={(evento) => setPassword(evento.target.value)}
            />
          </Campo>

          <Boton type="submit" anchoCompleto cargando={enviando} textoCargando="Verificando credenciales…">
            Iniciar sesión
          </Boton>
        </form>
      </div>
    </LayoutAutenticacion>
  );
}
