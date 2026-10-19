/** Contratos del catálogo académico (Microservicio A — `/api/carreras`, `/api/mallas`, `/api/asignaturas`). */

export interface Carrera {
  id: number;
  codigo: string;
  nombre: string;
  facultad: string;
  activa: boolean;
}

export interface CrearCarrera {
  codigo: string;
  nombre: string;
  facultad: string;
  activa: boolean;
}

export interface ActualizarCarrera {
  nombre: string;
  facultad: string;
  activa: boolean;
}

export interface Malla {
  id: number;
  carreraId: number;
  carreraCodigo: string;
  version: string;
  anio: number;
  fuente: string;
  rutaNormativa: string | null;
  vigente: boolean;
  totalAsignaturas: number;
}

export interface CrearMalla {
  carreraId: number;
  version: string;
  anio: number;
  fuente: string;
  rutaNormativa: string | null;
  vigente: boolean;
}

export interface UnidadTematica {
  numero: number;
  nombre: string;
  horas: number;
  contenidos: string;
}

export interface Asignatura {
  id: number;
  mallaId: number;
  codigo: string;
  nombre: string;
  creditos: number;
  horas: number;
  semestre: number;
  planCompleto: boolean;
  unidades: UnidadTematica[];
}

export interface CrearAsignatura {
  mallaId: number;
  codigo: string;
  nombre: string;
  creditos: number;
  horas: number;
  semestre: number;
  unidades: UnidadTematica[];
}

export interface ActualizarAsignatura {
  nombre: string;
  creditos: number;
  horas: number;
  semestre: number;
  unidades: UnidadTematica[];
}

/** Ítem de la carga masiva del catálogo estructurado. */
export interface AsignaturaImportacion {
  codigo: string;
  nombre: string;
  creditos: number;
  horas: number;
  semestre: number;
  unidades: UnidadTematica[];
}

/** Cuerpo de `POST /api/asignaturas/importar`. */
export interface ImportarCatalogo {
  mallaId: number;
  asignaturas: AsignaturaImportacion[];
}

/** Respuesta de `POST /api/asignaturas/importar`. */
export interface ResultadoImportacion {
  total: number;
  importadas: number;
  rechazadas: number;
  errores: string[];
}
