import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider'
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns'
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import { ThemeModeProvider } from './theme/ThemeModeProvider'
import { AuthProvider } from './lib/auth/AuthProvider'
import { createAppQueryClient } from './lib/api/queryClient'
import { SnackbarProvider } from './lib/notifications/SnackbarProvider'
import { router } from './app/router'
import './index.css'

const queryClient = createAppQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeModeProvider>
        <LocalizationProvider dateAdapter={AdapterDateFns}>
          <SnackbarProvider>
            <AuthProvider>
              <RouterProvider router={router} />
            </AuthProvider>
          </SnackbarProvider>
        </LocalizationProvider>
      </ThemeModeProvider>
    </QueryClientProvider>
  </StrictMode>,
)
