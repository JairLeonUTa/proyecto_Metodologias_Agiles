# Imagen de producción del frontend de SIHA-IA (Sprint 1).
# Etapa 1: compilación de la SPA con Vite.
FROM node:20-alpine AS compilacion

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# La URL del Gateway se resuelve en tiempo de compilación (Vite).
# Dentro de Docker se recomienda vaciarla para consumir rutas relativas a través de Nginx.
ARG VITE_API_URL=http://localhost:5000
ENV VITE_API_URL=$VITE_API_URL

RUN npm run build

# Etapa 2: servidor estático Nginx con fallback de SPA.
FROM nginx:1.27-alpine AS servidor

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=compilacion /app/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
