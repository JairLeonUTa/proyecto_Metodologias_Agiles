/// <reference types="vite/client" />

/**
 * Variables de entorno expuestas al cliente (Vite).
 * S1-A01: la URL del API Gateway se inyecta en tiempo de compilación.
 */
interface ImportMetaEnv {
  /** URL base del API Gateway (Ocelot). Valor por defecto: http://localhost:5000 */
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
