import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { ProveedorAutenticacion } from './context/AuthContext';
import './index.css';

const contenedor = document.getElementById('root');

if (!contenedor) {
  throw new Error('No se encontró el nodo #root para montar la aplicación SIHA-IA.');
}

createRoot(contenedor).render(
  <StrictMode>
    <BrowserRouter>
      <ProveedorAutenticacion>
        <App />
      </ProveedorAutenticacion>
    </BrowserRouter>
  </StrictMode>,
);
