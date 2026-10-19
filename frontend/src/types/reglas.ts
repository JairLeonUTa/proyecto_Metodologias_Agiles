/** Contratos de reglas normativas y umbrales heurísticos (Microservicio A — `/api/reglas`). */

export interface ParametroEvaluacion {
  id: number;
  rutaNormativa: string;
  theta: number;
  delta: number;
  tau: number;
  version: number;
  actualizadoUtc: string;
  actualizadoPor: string;
}

/** Cuerpo de `PUT /api/reglas/parametros/{rutaNormativa}`. */
export interface ActualizarParametros {
  theta: number;
  delta: number;
  tau: number;
}

export interface Regla {
  id: number;
  codigo: string;
  rutaNormativa: string;
  tipoTramite: string;
  descripcion: string;
  expresion: string | null;
  version: string;
  fuente: string;
  esNormativa: boolean;
  vigenteDesde: string;
  vigenteHasta: string | null;
  activa: boolean;
}

export interface CrearRegla {
  codigo: string;
  rutaNormativa: string;
  tipoTramite: string;
  descripcion: string;
  expresion: string | null;
  version: string;
  fuente: string;
  esNormativa: boolean;
  vigenteDesde: string;
  vigenteHasta: string | null;
}
