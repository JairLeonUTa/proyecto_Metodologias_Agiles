/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Paleta institucional de SIHA-IA (UTA).
        institucional: {
          50: '#eef4ff',
          100: '#d9e6ff',
          200: '#bcd3ff',
          300: '#8eb5ff',
          400: '#598cff',
          500: '#3462f5',
          600: '#1f43e0',
          700: '#1a34b5',
          800: '#1b2f90',
          900: '#1c2d73',
          950: '#141c46',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'Segoe UI', 'Roboto', 'Helvetica Neue', 'Arial', 'sans-serif'],
        mono: ['Consolas', 'ui-monospace', 'monospace'],
      },
      keyframes: {
        'aparecer': {
          '0%': { opacity: '0', transform: 'translateY(4px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
      },
      animation: {
        aparecer: 'aparecer 180ms ease-out',
      },
    },
  },
  plugins: [],
};
