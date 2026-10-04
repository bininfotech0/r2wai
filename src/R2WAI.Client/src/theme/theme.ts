import { createTheme, type ThemeOptions } from '@mui/material/styles'
import type {} from '@mui/x-data-grid/themeAugmentation'

/**
 * R2WAI Studio token set — purple/indigo primary, per the reference dashboard
 * mockups. Supersedes the old Blazor/MudBlazor blue (#2563EB) palette.
 * See: C:\Users\LENOVO\.claude\plans\not-use-blazor-server-mudblazor-serialized-plum.md
 */
const fontFamily = [
  'Inter',
  '-apple-system',
  'BlinkMacSystemFont',
  '"Segoe UI"',
  'Roboto',
  'sans-serif',
].join(',')

const shape = { borderRadius: 10 }

const typography: ThemeOptions['typography'] = {
  fontFamily,
  h1: { fontWeight: 720, letterSpacing: '-0.04em', lineHeight: 1.12 },
  h2: { fontWeight: 700, letterSpacing: '-0.035em', lineHeight: 1.18 },
  h3: { fontWeight: 700, letterSpacing: '-0.03em', lineHeight: 1.2 },
  h4: { fontWeight: 700, letterSpacing: '-0.025em', lineHeight: 1.22 },
  h5: { fontWeight: 650, letterSpacing: '-0.02em', lineHeight: 1.28 },
  h6: { fontWeight: 650, letterSpacing: '-0.01em', lineHeight: 1.35 },
  subtitle1: { fontWeight: 600, lineHeight: 1.45 },
  subtitle2: { fontWeight: 600, lineHeight: 1.45 },
  body1: { lineHeight: 1.55 },
  body2: { lineHeight: 1.55 },
  button: { fontWeight: 600, textTransform: 'none', letterSpacing: 0 },
  overline: { fontWeight: 700, letterSpacing: 0.9, lineHeight: 1.5 },
  caption: { lineHeight: 1.45 },
}

/** Soft, low-spread shadows — replaces MUI's default heavier/darker elevation stack. */
function buildShadows(isDark: boolean): ThemeOptions['shadows'] {
  const alpha = isDark ? 0.45 : 0.08
  const soft = `0 1px 2px rgba(15, 17, 21, ${alpha * 0.6}), 0 1px 3px rgba(15, 17, 21, ${alpha})`
  const raised = `0 2px 4px rgba(15, 17, 21, ${alpha * 0.7}), 0 4px 10px rgba(15, 17, 21, ${alpha})`
  const overlay = `0 8px 16px rgba(15, 17, 21, ${alpha * 0.8}), 0 12px 28px rgba(15, 17, 21, ${alpha * 1.3})`
  const shadows = Array(25).fill(raised) as ThemeOptions['shadows']
  shadows![0] = 'none'
  shadows![1] = soft
  shadows![2] = soft
  shadows![3] = raised
  shadows![8] = overlay
  shadows![24] = overlay
  return shadows
}

function buildComponents(isDark: boolean): ThemeOptions['components'] {
  const borderColor = isDark ? 'rgba(255,255,255,0.09)' : 'rgba(100,116,139,0.16)'
  return {
    MuiCssBaseline: {
      styleOverrides: {
        html: { minHeight: '100%', scrollBehavior: 'smooth' },
        body: {
          minHeight: '100%',
          fontSynthesis: 'none',
          textRendering: 'optimizeLegibility',
          WebkitFontSmoothing: 'antialiased',
          MozOsxFontSmoothing: 'grayscale',
        },
        '#root': { minHeight: '100vh' },
        '::selection': {
          backgroundColor: isDark ? 'rgba(165,180,252,.28)' : 'rgba(99,102,241,.18)',
        },
        '*': { scrollbarWidth: 'thin' },
        '*::-webkit-scrollbar': { width: 8, height: 8 },
        '*::-webkit-scrollbar-thumb': {
          backgroundColor: isDark ? 'rgba(255,255,255,0.18)' : 'rgba(15,17,21,0.18)',
          borderRadius: 8,
        },
        '*::-webkit-scrollbar-track': { backgroundColor: 'transparent' },
        '@media (prefers-reduced-motion: reduce)': {
          '*, *::before, *::after': {
            animationDuration: '0.01ms !important',
            animationIterationCount: '1 !important',
            scrollBehavior: 'auto !important',
            transitionDuration: '0.01ms !important',
          },
        },
      },
    },
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: {
          minHeight: 40,
          borderRadius: 10,
          paddingInline: 16,
          fontWeight: 600,
          transition: 'background-color 140ms ease, border-color 140ms ease, box-shadow 140ms ease, transform 140ms ease',
          '&:focus-visible': {
            outline: `3px solid ${isDark ? 'rgba(165,180,252,.42)' : 'rgba(99,102,241,.3)'}`,
            outlineOffset: 2,
          },
          '&:active:not(:disabled)': { transform: 'translateY(1px)' },
        },
        sizeSmall: { minHeight: 34, paddingInline: 12 },
        sizeMedium: { paddingBlock: 8 },
        contained: {
          '&:hover': { filter: 'brightness(.97)', boxShadow: '0 5px 14px rgba(15,23,42,.12)' },
          '&:disabled': { opacity: 0.52 },
        },
        outlined: {
          borderColor,
          '&:hover': { borderColor: isDark ? 'rgba(165,180,252,.5)' : 'rgba(99,102,241,.42)' },
        },
      },
    },
    MuiIconButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          transition: 'background-color 140ms ease, color 140ms ease, transform 140ms ease',
          '&:focus-visible': {
            outline: `3px solid ${isDark ? 'rgba(165,180,252,.42)' : 'rgba(99,102,241,.3)'}`,
            outlineOffset: 1,
          },
          '&:active:not(:disabled)': { transform: 'scale(.96)' },
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
        rounded: { borderRadius: 16 },
        outlined: { borderColor, borderRadius: 16 },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 16,
          border: `1px solid ${borderColor}`,
          transition: 'box-shadow 160ms ease, border-color 160ms ease, transform 160ms ease',
        },
      },
    },
    MuiCardActionArea: {
      styleOverrides: {
        root: {
          '&:hover': {
            boxShadow: isDark
              ? '0 4px 14px rgba(0,0,0,0.4)'
              : '0 4px 14px rgba(15,17,21,0.1)',
          },
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          boxShadow: 'none',
          borderBottom: `1px solid ${borderColor}`,
          backdropFilter: 'blur(16px)',
          backgroundColor: isDark ? 'rgba(23,25,35,.9)' : 'rgba(255,255,255,.9)',
        },
      },
    },
    MuiBreadcrumbs: {
      styleOverrides: {
        root: { fontSize: '0.8125rem' },
        separator: { color: isDark ? 'rgba(255,255,255,.34)' : 'rgba(15,23,42,.3)', marginInline: 8 },
      },
    },
    MuiTabs: {
      styleOverrides: {
        indicator: { borderRadius: 3, height: 3 },
      },
    },
    MuiTab: {
      styleOverrides: {
        root: { textTransform: 'none', fontWeight: 600, minHeight: 44 },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { borderRadius: 999, fontWeight: 600 },
        sizeSmall: { fontSize: '0.75rem' },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: {
          '&:hover': {
            backgroundColor: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(99,102,241,0.04)',
          },
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          fontWeight: 650,
          fontSize: '0.75rem',
          letterSpacing: '.035em',
          color: isDark ? 'rgba(255,255,255,0.68)' : 'rgba(15,17,21,0.64)',
          backgroundColor: isDark ? 'rgba(255,255,255,.025)' : 'rgba(15,23,42,.018)',
        },
        root: { borderColor, paddingBlock: 12 },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          transition: 'box-shadow 140ms ease',
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: isDark ? 'rgba(165,180,252,.52)' : 'rgba(99,102,241,.45)' },
          '&.Mui-focused': {
            boxShadow: `0 0 0 3px ${isDark ? 'rgba(165,180,252,.12)' : 'rgba(99,102,241,.09)'}`,
          },
        },
        notchedOutline: { borderColor, transition: 'border-color 140ms ease' },
      },
    },
    MuiInputLabel: {
      styleOverrides: { root: { fontSize: '0.9rem' } },
    },
    MuiFormHelperText: {
      styleOverrides: { root: { marginInline: 2, lineHeight: 1.45 } },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 8,
          transition: 'background-color 120ms ease',
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: 18,
          border: `1px solid ${borderColor}`,
          boxShadow: isDark ? '0 24px 70px rgba(0,0,0,.48)' : '0 24px 70px rgba(15,23,42,.18)',
        },
      },
    },
    MuiDialogTitle: {
      styleOverrides: { root: { fontWeight: 700, letterSpacing: '-.02em', padding: '22px 24px 10px' } },
    },
    MuiDialogActions: {
      styleOverrides: { root: { padding: '12px 24px 22px', gap: 8 } },
    },
    MuiMenu: {
      styleOverrides: {
        paper: {
          border: `1px solid ${borderColor}`,
          borderRadius: 14,
          boxShadow: isDark ? '0 12px 32px rgba(0,0,0,.36)' : '0 12px 32px rgba(15,23,42,.14)',
        },
      },
    },
    MuiMenuItem: {
      styleOverrides: { root: { minHeight: 40, borderRadius: 8, marginInline: 6, paddingInline: 10 } },
    },
    MuiAlert: {
      styleOverrides: {
        root: { borderRadius: 12, alignItems: 'flex-start' },
        message: { lineHeight: 1.5 },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: { border: 0, borderRadius: 0 },
        columnHeaders: { borderBottom: `1px solid ${borderColor}`, backgroundColor: isDark ? 'rgba(255,255,255,.025)' : 'rgba(15,23,42,.018)' },
        columnHeaderTitle: { fontWeight: 650, fontSize: '0.75rem', letterSpacing: '.025em' },
        cell: { borderColor, fontSize: '0.875rem' },
        footerContainer: { minHeight: 56, borderTop: `1px solid ${borderColor}` },
      },
    },
    MuiTooltip: {
      styleOverrides: {
        tooltip: { borderRadius: 6, fontWeight: 500 },
      },
    },
  }
}

const lightOptions: ThemeOptions = {
  palette: {
    mode: 'light',
    primary: { main: '#6366F1', light: '#818CF8', dark: '#4F46E5' },
    secondary: { main: '#7C3AED' },
    success: { main: '#16A34A' },
    warning: { main: '#D97706' },
    error: { main: '#DC2626' },
    text: { primary: '#171B28', secondary: '#687386', disabled: '#9AA3B2' },
    background: { default: '#F6F8FC', paper: '#FFFFFF' },
    divider: 'rgba(100,116,139,0.16)',
  },
  typography,
  shape,
  shadows: buildShadows(false),
  components: buildComponents(false),
}

const darkOptions: ThemeOptions = {
  palette: {
    mode: 'dark',
    primary: { main: '#818CF8', light: '#A5B4FC', dark: '#6366F1' },
    secondary: { main: '#A78BFA' },
    success: { main: '#22C55E' },
    warning: { main: '#F59E0B' },
    error: { main: '#EF4444' },
    text: { primary: '#F1F3F8', secondary: '#A7B0C0', disabled: '#6F7888' },
    background: { default: '#0D1118', paper: '#151A23' },
    divider: 'rgba(255,255,255,0.08)',
  },
  typography,
  shape,
  shadows: buildShadows(true),
  components: buildComponents(true),
}

export const lightTheme = createTheme(lightOptions)
export const darkTheme = createTheme(darkOptions)
