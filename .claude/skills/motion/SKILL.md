---
name: motion
description: "UI motion/animation rules for the R2WAI React client (MUI 9). Use when adding or changing transitions, animations, hover/press feedback, enter/exit effects, loading shimmer, drawer/dialog/list motion, or anything in src/R2WAI.Client that moves. Triggers: animate, animation, motion, transition, fade, slide, collapse, hover effect, micro-interaction, skeleton, shimmer."
metadata:
  version: "0.1.0"
---

# Motion (R2WAI Client)

Motion in R2WAI is **quiet, fast, functional**. It confirms an action, shows where something came from, or signals loading. It never decorates.

## Stack — no new dependencies

- Use **MUI transitions** (`Fade`, `Grow`, `Collapse`, `Slide`, `Zoom`) and `theme.transitions.create(...)`.
- Use `sx` / theme `styleOverrides` for CSS transitions and `@keyframes` (via `keyframes` from `@mui/material/styles` / `@emotion/react`).
- Do **not** add `framer-motion`/`motion`, GSAP, or animate.css unless the user explicitly asks.
- `@xyflow/react` canvases and `recharts` keep their own built-in animation; don't layer extra motion on them.

## Tokens (match `src/R2WAI.Client/src/theme/theme.ts`)

| Use | Duration | Easing |
|---|---|---|
| Hover / color / border / press | 140ms | `ease` |
| Card / surface lift | 160ms | `ease` |
| Expand / collapse, small enter | `theme.transitions.duration.shorter` (200ms) | `easeInOut` |
| Drawer, dialog, layout shift | `theme.transitions.duration.standard` (225–300ms) | `sharp` / `easeOut` (enter), `easeIn` (exit) |

Rules:
- Nothing user-triggered > 300ms. Exits faster than enters.
- Animate only `opacity`, `transform`, `background-color`, `border-color`, `box-shadow`, `color`. Never animate `width/height/top/left` except via MUI `Collapse` or existing layout transitions (`MainLayout` margin/width).
- Press feedback already exists globally: buttons `translateY(1px)`, icon buttons `scale(.96)`. Don't duplicate per component.
- Hover lift on cards: max `translateY(-2px)` + shadow step; only on clickable cards.

## Reduced motion

`MuiCssBaseline` already forces near-zero durations under `prefers-reduced-motion: reduce`. So:
- Never use `!important` durations that would override it.
- For JS-driven motion (timers, scroll-into-view, auto-advancing), check `useMediaQuery('(prefers-reduced-motion: reduce)')` and skip.
- Infinite animations (pulse, spin, shimmer) must stop or degrade to static under reduced motion.

## Patterns

**Conditional panel**
```tsx
<Collapse in={open} timeout="auto" unmountOnExit>…</Collapse>
```

**Swap content without flash** — key a `Fade` on the data identity:
```tsx
<Fade in key={run.id} timeout={200}><Box>…</Box></Fade>
```

**Custom transition in sx**
```tsx
sx={{ transition: (t) => t.transitions.create(['opacity', 'transform'], { duration: t.transitions.duration.shorter }) }}
```

**Live status (running/streaming)** — subtle opacity pulse on the badge dot only, 1.6s, `ease-in-out`, infinite; no scaling text.

**Loading** — prefer MUI `Skeleton` (`animation="wave"`) sized like the final content; avoid spinners for > 300ms page loads.

**Lists** — don't stagger-animate DataGrid rows or long lists. Staggered entry is allowed only for ≤ 8 dashboard tiles, 30ms step, 200ms each, first render only.

## Don'ts

- No motion on route change beyond a 150ms fade of the content area.
- No parallax, bounce/elastic easing, auto-playing carousels, or scroll-jacking.
- No layout-shifting animation inside forms while the user is typing.
- No animation as the only signal of state — pair with text/icon/color (a11y).

## Checklist before finishing

1. Durations/easing come from the token table or `theme.transitions`.
2. Works with OS reduced-motion on (verify DevTools → Rendering → emulate `prefers-reduced-motion`).
3. Dark and light mode both look correct mid-transition.
4. `npm run build` and `npm run lint` pass in `src/R2WAI.Client`.
