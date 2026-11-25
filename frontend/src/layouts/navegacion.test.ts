import { describe, expect, it } from 'vitest';
import { puedeVerRuta } from './navegacion';

describe('puedeVerRuta', () => {
  it('usa los permisos configurados en App para rutas protegidas', () => {
    expect(puedeVerRuta('/administracion/usuarios', 'OPERADOR')).toBe(false);
    expect(puedeVerRuta('/administracion/usuarios', 'ADMINISTRADOR')).toBe(true);
    expect(puedeVerRuta('/solicitudes', 'DOCENTE')).toBe(true);
  });

  it('no confunde una ruta fija con una de id', () => {
    expect(puedeVerRuta('/solicitudes/nueva', 'DOCENTE')).toBe(false);
    expect(puedeVerRuta('/solicitudes/nueva', 'OPERADOR')).toBe(true);
    expect(puedeVerRuta('/solicitudes/90039b52-7056-4c00-8f63-2555280879e0', 'DOCENTE')).toBe(true);
    expect(puedeVerRuta('/expedientes/consulta', 'DOCENTE')).toBe(false);
  });

  it('nunca devuelve al login ni a acceso denegado', () => {
    expect(puedeVerRuta('/login', 'ADMINISTRADOR')).toBe(false);
    expect(puedeVerRuta('/acceso-denegado', 'ADMINISTRADOR')).toBe(false);
  });
});
