/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // ── Framed-sheet tokens ──────────────────────────────────────
        // Chrome (sidebar + top bar share this tinted ground)
        frame: '#f6f7f9',
        raised: '#eef0f4',
        active: '#e3e8ef',
        edge: '#dde2ea',
        rule: '#ebeef3',
        control: '#b9c1cd',

        // Ink
        ink: '#111a26',
        muted: '#4c5665',
        faint: '#6b7686',

        // The one accent hue — budgeted to <3% of pixels.
        // Rampa construida con los colores del logo: azul oscuro (900),
        // azul (600) y turquesa (marca `turquesa`).
        institucional: {
          50: '#eef4fc',
          100: '#d8e7fa',
          200: '#b0cff4',
          300: '#7aabe9',
          400: '#4183d7',
          500: '#1164c4',
          600: '#0060d0',
          700: '#004ba6',
          800: '#023c7f',
          900: '#002049',
          950: '#001630',
        },

        // Turquesa del logo. Solo en superficies grandes (cabecera pública):
        // sobre blanco no alcanza contraste para texto.
        turquesa: '#00a0b1',

        // Semantic
        positive: '#1e7b4f',
        negative: '#c4242a',
        warning: '#92400e',

        // Status pill pairs
        'pill-pos': { bg: '#dcfae5', ink: '#04250e' },
        'pill-neg': { bg: '#ffedea', ink: '#831c1e' },
        'pill-warn': { bg: '#fffbeb', ink: '#92400e' },
        'pill-neutral': { bg: '#e3e8ef', ink: '#39424f' },

        // Map stone/slate to the cool neutral ramp so existing classes work
        stone: {
          50: '#f6f7f9',
          100: '#eef0f4',
          200: '#dde2ea',
          300: '#ccd3dd',
          400: '#9aa4b2',
          500: '#6b7686',
          600: '#4c5665',
          700: '#39424f',
          800: '#262e3a',
          900: '#111a26',
          950: '#0b111b',
        },
        slate: {
          50: '#f6f7f9',
          100: '#eef0f4',
          200: '#dde2ea',
          300: '#ccd3dd',
          400: '#9aa4b2',
          500: '#6b7686',
          600: '#4c5665',
          700: '#39424f',
          800: '#262e3a',
          900: '#111a26',
          950: '#0b111b',
        },
      },

      fontFamily: {
        sans: ['"IBM Plex Sans"', 'Inter', 'system-ui', 'sans-serif'],
        mono: ['"IBM Plex Mono"', 'Consolas', 'ui-monospace', 'monospace'],
        // Alias so existing font-display classes resolve to the one grotesk
        display: ['"IBM Plex Sans"', 'Inter', 'system-ui', 'sans-serif'],
      },

      keyframes: {
        aparecer: {
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
