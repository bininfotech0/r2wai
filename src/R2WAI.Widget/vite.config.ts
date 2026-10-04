import { defineConfig } from 'vite'

// Fully separate build from R2WAI.Client — no MUI/Router/MSAL, no shared
// deps. IIFE output so a bare <script src="/widget/widget.js"> just works
// on a third-party page with no bundler/module-system assumptions.
export default defineConfig({
  build: {
    outDir: 'dist',
    lib: {
      entry: 'src/widget.ts',
      name: 'R2WAIWidget',
      formats: ['iife'],
      fileName: () => 'widget.js',
    },
    rollupOptions: {
      output: {
        // Predictable filename (no content hash) — the embed snippet
        // (see ChatbotEmbedDialog) references a fixed /widget/widget.js path.
        assetFileNames: 'widget.[ext]',
      },
    },
    cssCodeSplit: false,
    minify: true,
  },
})
