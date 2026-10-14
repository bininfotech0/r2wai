import { createTheme, type ThemeOptions } from '@mui/material/styles'
import type {} from '@mui/x-data-grid/themeAugmentation'

/**
 * R2WAI Studio tokens, in the Copilot Studio / Fluent 2 visual language: a neutral grey canvas
 * with white surfaces, 1px neutral borders instead of shadows, 8px corners on surfaces and 4–6px
 * on controls, Segoe UI, and compact 32px controls. Colour comes from the R2WAI brand palette
 * (`brand` below); only layout and density follow Fluent. Touch screens still get 44px targets (touchTarget/touchRow).
 *
 * shape.borderRadius is the unit `sx` multiplies numeric radii by, so lowering it to 8 flattens
 * every component's `borderRadius: 2`-style value in proportion.
 */
const fontFamily = [
  '"Segoe UI Variable Text"',
  '"Segoe UI"',
  'Inter',
  '-apple-system',
  'BlinkMacSystemFont',
  'Roboto',
  'sans-serif',
].join(',')

const shape = { borderRadius: 8 }

/**
 * R2WAI brand palette, exact values, for marks and illustrations (the favicon uses them too).
 * For text, UI state and chart series use the theme palette instead: it maps purple→primary,
 * pink→secondary, cyan→info, lime→success, orange→warning and darkens/lightens each per mode
 * just enough to clear WCAG AA (4.5:1).
 */
export const brand = {
  pink: '#EE5091',
  cyan: '#199FD7',
  lime: '#99BD3C',
  orange: '#FC7942',
  purple: '#8A50D8',
} as const

const typography: ThemeOptions['typography'] = {
  fontFamily,
  h1: { fontWeight: 600, letterSpacing: '-0.02em', lineHeight: 1.15 },
  h2: { fontWeight: 600, letterSpacing: '-0.015em', lineHeight: 1.2 },
  h3: { fontWeight: 600, letterSpacing: '-0.01em', lineHeight: 1.25 },
  h4: { fontWeight: 600, letterSpacing: '-0.01em', lineHeight: 1.28 },
  h5: { fontWeight: 600, letterSpacing: 0, lineHeight: 1.33 },
  h6: { fontWeight: 600, letterSpacing: 0, lineHeight: 1.4 },
  subtitle1: { fontWeight: 600, lineHeight: 1.45 },
  subtitle2: { fontWeight: 600, lineHeight: 1.45 },
  body1: { lineHeight: 1.5 },
  body2: { fontSize: '0.875rem', lineHeight: 1.43 },
  button: { fontWeight: 600, textTransform: 'none', letterSpacing: 0 },
  overline: { fontWeight: 700, letterSpacing: 0.9, lineHeight: 1.5 },
  caption: { lineHeight: 1.45 },
}

/** Soft, low-spread shadows — replaces MUI's default heavier/darker elevation stack. */
function buildShadows(isDark: boolean): ThemeOptions['shadows'] {
  const alpha = isDark ? 0.4 : 0.06
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

/**
 * Touch screens only (`pointer: coarse`): lift controls to the 44px minimum tap target.
 * Mouse layouts keep their compact density untouched.
 */
const coarsePointer = '@media (pointer: coarse)'
const touchTarget = { [coarsePointer]: { minWidth: 44, minHeight: 44 } }
const touchRow = { [coarsePointer]: { minHeight: 44 } }

function buildComponents(isDark: boolean): ThemeOptions['components'] {
  const borderColor = isDark ? '#3D3D3D' : '#E0E0E0'
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
          backgroundColor: isDark ? 'rgba(167,124,226,.28)' : 'rgba(138,80,216,.18)',
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
          minHeight: 32,
          borderRadius: 6,
          paddingInline: 12,
          fontWeight: 600,
          ...touchRow,
          transition: 'background-color 140ms ease, border-color 140ms ease, box-shadow 140ms ease, transform 140ms ease',
          '&:focus-visible': {
            outline: `3px solid ${isDark ? 'rgba(167,124,226,.42)' : 'rgba(138,80,216,.3)'}`,
            outlineOffset: 2,
          },
          '&:active:not(:disabled)': { transform: 'translateY(1px)' },
        },
        sizeSmall: { minHeight: 28, paddingInline: 10, ...touchRow },
        sizeMedium: { paddingBlock: 4 },
        sizeLarge: { minHeight: 40, paddingInline: 16 },
        contained: {
          '&:hover': { filter: 'brightness(.95)', boxShadow: 'none' },
          '&:disabled': { opacity: 0.52 },
        },
        outlined: {
          borderColor: isDark ? '#666666' : '#D1D1D1',
          '&:hover': { borderColor: isDark ? '#ADADAD' : '#C7C7C7', backgroundColor: isDark ? '#3D3D3D' : '#F5F5F5' },
        },
      },
    },
    MuiIconButton: {
      styleOverrides: {
        root: {
          borderRadius: 6,
          transition: 'background-color 140ms ease, color 140ms ease, transform 140ms ease',
          '&:focus-visible': {
            outline: `3px solid ${isDark ? 'rgba(167,124,226,.42)' : 'rgba(138,80,216,.3)'}`,
            outlineOffset: 1,
          },
          '&:active:not(:disabled)': { transform: 'scale(.96)' },
          ...touchTarget,
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
        rounded: { borderRadius: 8 },
        outlined: { borderColor, borderRadius: 8 },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 8,
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
              ? '0 2px 8px rgba(0,0,0,0.35)'
              : '0 2px 8px rgba(0,0,0,0.08)',
          },
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          boxShadow: 'none',
          borderBottom: `1px solid ${borderColor}`,
          backgroundColor: isDark ? '#292929' : '#FFFFFF',
        },
      },
    },
    // The top bar spans the full width above the nav rail (MainLayout), one layer above
    // zIndex.drawer. Overlay drawers (Settings, Test pane, tool picker, run inspector) must open
    // above it or their title and close button sit under the bar. The permanent rail sets its own.
    MuiDrawer: {
      styleOverrides: {
        root: ({ ownerState, theme }) => (ownerState.variant === 'temporary' || ownerState.variant === undefined
          ? { zIndex: theme.zIndex.modal }
          : {}),
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
            backgroundColor: isDark ? '#2E2E2E' : '#F5F5F5',
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
          backgroundColor: isDark ? '#2E2E2E' : '#FAFAFA',
        },
        root: { borderColor, paddingBlock: 8 },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 6,
          transition: 'box-shadow 140ms ease',
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: isDark ? 'rgba(167,124,226,.52)' : 'rgba(138,80,216,.45)' },
          '&.Mui-focused': {
            boxShadow: `0 0 0 3px ${isDark ? 'rgba(167,124,226,.12)' : 'rgba(138,80,216,.09)'}`,
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
          borderRadius: 6,
          transition: 'background-color 120ms ease',
          ...touchRow,
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: 12,
          border: `1px solid ${borderColor}`,
          boxShadow: isDark ? '0 16px 48px rgba(0,0,0,.48)' : '0 16px 48px rgba(0,0,0,.14)',
        },
      },
    },
    MuiDialogTitle: {
      styleOverrides: { root: { fontWeight: 600, letterSpacing: 0, padding: '20px 24px 8px' } },
    },
    MuiDialogActions: {
      styleOverrides: { root: { padding: '12px 24px 22px', gap: 8 } },
    },
    MuiMenu: {
      styleOverrides: {
        paper: {
          border: `1px solid ${borderColor}`,
          borderRadius: 8,
          boxShadow: isDark ? '0 8px 24px rgba(0,0,0,.4)' : '0 8px 24px rgba(0,0,0,.12)',
        },
      },
    },
    MuiMenuItem: {
      styleOverrides: { root: { minHeight: 36, borderRadius: 4, marginInline: 4, paddingInline: 10, ...touchRow } },
    },
    MuiAlert: {
      styleOverrides: {
        root: { borderRadius: 8, alignItems: 'flex-start' },
        message: { lineHeight: 1.5 },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: { border: 0, borderRadius: 0 },
        columnHeaders: { borderBottom: `1px solid ${borderColor}`, backgroundColor: isDark ? '#2E2E2E' : '#FAFAFA' },
        columnHeaderTitle: { fontWeight: 650, fontSize: '0.75rem', letterSpacing: '.025em' },
        cell: { borderColor, fontSize: '0.875rem' },
        footerContainer: { minHeight: 56, borderTop: `1px solid ${borderColor}` },
      },
    },
    MuiTooltip: {
      styleOverrides: {
        tooltip: { borderRadius: 4, fontWeight: 500 },
      },
    },
  }
}

const lightOptions: ThemeOptions = {
  palette: {
    mode: 'light',
    // Light-mode mains clear WCAG AA (4.5:1) as text on both paper and the page background; where
    // the brand hex itself falls short it moves to `light` and `main` is the same hue, darkened.
    primary: { main: brand.purple, light: '#A77CE2', dark: '#6E3FB0' },
    secondary: { main: '#BE4074', light: brand.pink, dark: '#9A3460' },
    info: { main: '#1377A1', light: brand.cyan, dark: '#0F6085' },
    success: { main: '#607726', light: brand.lime, dark: '#4C5E1E' },
    warning: { main: '#B0552E', light: brand.orange, dark: '#8E4425' },
    error: { main: '#DC2626' },
    text: { primary: '#242424', secondary: '#616161', disabled: '#9E9E9E' },
    background: { default: '#F5F5F5', paper: '#FFFFFF' },
    divider: '#E0E0E0',
  },
  typography,
  shape,
  shadows: buildShadows(false),
  components: buildComponents(false),
}

const darkOptions: ThemeOptions = {
  palette: {
    mode: 'dark',
    primary: { main: '#A77CE2', light: '#C2A5EB', dark: brand.purple },
    secondary: { main: '#EF5997', light: '#F48BB6', dark: brand.pink },
    info: { main: brand.cyan, light: '#5EBBE3', dark: '#1377A1' },
    success: { main: brand.lime, light: '#B8D277', dark: '#7A9730' },
    warning: { main: brand.orange, light: '#FDA17B', dark: '#C9612F' },
    error: { main: '#EF4444' },
    text: { primary: '#FFFFFF', secondary: '#ADADAD', disabled: '#757575' },
    background: { default: '#1F1F1F', paper: '#292929' },
    divider: '#3D3D3D',
  },
  typography,
  shape,
  shadows: buildShadows(true),
  components: buildComponents(true),
}

export const lightTheme = createTheme(lightOptions)
export const darkTheme = createTheme(darkOptions)
