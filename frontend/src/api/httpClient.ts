import { leerToken } from './almacenamiento';

/**
 * Cliente HTTP centralizado de SIHA-IA (tarea S1-A01).
 *
 * Responsabilidades:
 *  - Resolver la URL base del API Gateway desde `VITE_API_URL` (por defecto `http://localhost:5000`).
 *  - Inyectar automáticamente la cabecera `Authorization: Bearer <token>`.
 *  - Normalizar los errores del backend (`{ mensaje }`, `ValidationProblemDetails`, etc.).
 *  - Notificar globalmente los 401 para cerrar la sesión del usuario.
 */

const URL_BASE = (import.meta.env.VITE_API_URL ?? 'http://localhost:5000').replace(/\/+$/, '');

export type MetodoHttp = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

/** Valor admitido como parámetro de query. */
export type ValorParametro = string | number | boolean | null | undefined;

export interface OpcionesPeticion {
  metodo?: MetodoHttp;
  cuerpo?: unknown;
  parametros?: Record<string, ValorParametro>;
  /** Omite la cabecera `Authorization` (usado en el login). */
  sinAutorizacion?: boolean;
  /** Señal de cancelación opcional para peticiones cancelables. */
  senal?: AbortSignal;
}

/**
 * Error normalizado del API. Conserva el código HTTP y los detalles de validación
 * para que la UI pueda mostrar mensajes consistentes.
 */
export class ErrorApi extends Error {
  public readonly estado: number;
  public readonly detalles: string[];

  constructor(mensaje: string, estado: number, detalles: string[] = []) {
    super(mensaje);
    this.name = 'ErrorApi';
    this.estado = estado;
    this.detalles = detalles;
  }

  /** `true` cuando el backend rechazó la petición por datos inválidos. */
  get esValidacion(): boolean {
    return this.estado === 400 || this.estado === 422;
  }

  /** `true` cuando el rol autenticado no tiene permisos suficientes (RBAC). */
  get esProhibido(): boolean {
    return this.estado === 403;
  }

  /** `true` cuando no hubo respuesta del servidor (Gateway caído o CORS). */
  get esRed(): boolean {
    return this.estado === 0;
  }
}

/** Manejador global invocado cuando el backend responde 401. */
type ManejadorNoAutorizado = () => void;

let manejadorNoAutorizado: ManejadorNoAutorizado | null = null;

/**
 * Registra la acción a ejecutar ante un 401 (cerrar sesión). El `AuthProvider`
 * es quien lo registra, evitando dependencias circulares con el contexto de React.
 */
export function registrarManejadorNoAutorizado(manejador: ManejadorNoAutorizado | null): void {
  manejadorNoAutorizado = manejador;
}

/** URL base efectiva del Gateway (expuesta para diagnósticos en la UI). */
export const urlBaseApi = URL_BASE;

function construirUrl(ruta: string, parametros?: Record<string, ValorParametro>): string {
  const url = `${URL_BASE}${ruta.startsWith('/') ? ruta : `/${ruta}`}`;
  if (!parametros) {
    return url;
  }

  const busqueda = new URLSearchParams();
  for (const [clave, valor] of Object.entries(parametros)) {
    if (valor === undefined || valor === null || valor === '') {
      continue;
    }
    busqueda.append(clave, String(valor));
  }

  const cadena = busqueda.toString();
  return cadena ? `${url}?${cadena}` : url;
}

function extraerMensaje(cuerpo: unknown, estado: number): { mensaje: string; detalles: string[] } {
  const generico = `La operación falló con el código HTTP ${estado}.`;

  if (typeof cuerpo === 'string' && cuerpo.trim()) {
    return { mensaje: cuerpo.trim(), detalles: [] };
  }

  if (cuerpo && typeof cuerpo === 'object') {
    const datos = cuerpo as Record<string, unknown>;

    // Respuestas de los controladores de SIHA-IA: { mensaje: "..." }
    if (typeof datos.mensaje === 'string' && datos.mensaje.trim()) {
      return { mensaje: datos.mensaje.trim(), detalles: [] };
    }

    // `ValidationProblemDetails` de ASP.NET Core: { errors: { campo: ["..."] } }
    if (datos.errors && typeof datos.errors === 'object') {
      const detalles: string[] = [];
      for (const [campo, valor] of Object.entries(datos.errors as Record<string, unknown>)) {
        const textos = Array.isArray(valor) ? valor.map(String) : [String(valor)];
        for (const texto of textos) {
          detalles.push(campo && campo !== '$' ? `${campo}: ${texto}` : texto);
        }
      }
      if (detalles.length > 0) {
        return { mensaje: 'La validación del servidor rechazó la solicitud.', detalles };
      }
    }

    if (typeof datos.detail === 'string' && datos.detail.trim()) {
      return { mensaje: datos.detail.trim(), detalles: [] };
    }

    if (typeof datos.title === 'string' && datos.title.trim()) {
      return { mensaje: datos.title.trim(), detalles: [] };
    }
  }

  if (estado === 403) {
    return { mensaje: 'No tiene permisos suficientes para realizar esta operación.', detalles: [] };
  }

  if (estado === 404) {
    return { mensaje: 'El recurso solicitado no existe.', detalles: [] };
  }

  if (estado >= 500) {
    return { mensaje: 'El servicio presentó un error interno. Intente nuevamente en unos instantes.', detalles: [] };
  }

  return { mensaje: generico, detalles: [] };
}

/**
 * Ejecuta una petición contra el API Gateway y devuelve la respuesta tipada.
 * Lanza `ErrorApi` ante cualquier fallo, de forma que las capas superiores
 * siempre gestionen un único tipo de error.
 */
export async function peticion<T>(ruta: string, opciones: OpcionesPeticion = {}): Promise<T> {
  const { metodo = 'GET', cuerpo, parametros, sinAutorizacion = false, senal } = opciones;

  const cabeceras: Record<string, string> = { Accept: 'application/json' };
  const token = leerToken();

  if (token && !sinAutorizacion) {
    cabeceras.Authorization = `Bearer ${token}`;
  }

  if (cuerpo !== undefined) {
    cabeceras['Content-Type'] = 'application/json';
  }

  let respuesta: Response;
  try {
    const opcionesFetch: RequestInit = { method: metodo, headers: cabeceras };
    if (cuerpo !== undefined) {
      opcionesFetch.body = JSON.stringify(cuerpo);
    }
    if (senal) {
      opcionesFetch.signal = senal;
    }

    respuesta = await fetch(construirUrl(ruta, parametros), opcionesFetch);
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    throw new ErrorApi(
      `No se pudo contactar el API Gateway en ${URL_BASE}. Verifique que los servicios estén en ejecución.`,
      0,
    );
  }

  const sinContenido = respuesta.status === 204 || respuesta.status === 205;
  let payload: unknown = null;

  if (!sinContenido) {
    const texto = await respuesta.text();
    if (texto) {
      try {
        payload = JSON.parse(texto) as unknown;
      } catch {
        payload = texto;
      }
    }
  }

  if (respuesta.status === 401 && !sinAutorizacion) {
    manejadorNoAutorizado?.();
    throw new ErrorApi('La sesión expiró o el token no es válido. Inicie sesión nuevamente.', 401);
  }

  if (!respuesta.ok) {
    const { mensaje, detalles } = extraerMensaje(payload, respuesta.status);
    throw new ErrorApi(mensaje, respuesta.status, detalles);
  }

  return payload as T;
}

/** Extrae un mensaje legible de cualquier error, incluidos los de red. */
export function mensajeDeError(error: unknown): string {
  if (error instanceof ErrorApi) {
    return error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return 'Ocurrió un error inesperado.';
}

/** Extrae los detalles de validación de un error, si existen. */
export function detallesDeError(error: unknown): string[] {
  return error instanceof ErrorApi ? error.detalles : [];
}
