import { useEffect } from 'react';

/** Fija el título del documento con el sufijo institucional. */
export function useTituloPagina(titulo: string): void {
  useEffect(() => {
    document.title = `${titulo} | Homologa UTA`;
  }, [titulo]);
}
