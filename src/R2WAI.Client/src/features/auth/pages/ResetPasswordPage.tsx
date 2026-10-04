import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  Alert,
  Box,
  Button,
  InputAdornment,
  Link as MuiLink,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { TooltipIconButton as IconButton } from '../../../components/TooltipIconButton'
import VisibilityIcon from '@mui/icons-material/Visibility'
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'
import { resetPasswordRequest } from '../../../lib/auth/authClient'
import { useDocumentTitle } from '../../../lib/useDocumentTitle'

const schema = z
  .object({
    email: z.string().email('Enter a valid email address'),
    token: z.string().min(1, 'Reset code is required'),
    newPassword: z.string().min(8, 'Password must be at least 8 characters'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })

type FormValues = z.infer<typeof schema>

export function ResetPasswordPage() {
  useDocumentTitle('Reset Password · R2WAI Studio')
  const navigate = useNavigate()
  const location = useLocation()
  const locationState = location.state as { email?: unknown } | null
  const initialEmail = typeof locationState?.email === 'string' ? locationState.email : ''
  const [success, setSuccess] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: initialEmail } })

  async function onSubmit(values: FormValues) {
    setError(null)
    setIsSubmitting(true)
    try {
      await resetPasswordRequest(values.email, values.token, values.newPassword)
      setSuccess(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to reset password.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Paper elevation={0} sx={{ p: 4, width: 400, maxWidth: '90vw' }}>
      <Typography variant="h6" sx={{ mb: 0.5, fontWeight: 700 }}>
        New Password
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Enter the reset code and your new password.
      </Typography>

      {success ? (
        <Stack spacing={2}>
          <Alert severity="success">Your password has been reset successfully.</Alert>
          <Button variant="contained" size="large" fullWidth onClick={() => navigate('/login')}>
            Go to Login
          </Button>
        </Stack>
      ) : (
        <Box component="form" onSubmit={(e) => void handleSubmit(onSubmit)(e)} noValidate>
          <Stack spacing={2}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label="Email address"
              type="email"
              fullWidth
              autoFocus
              autoComplete="email"
              inputMode="email"
              error={!!errors.email}
              helperText={errors.email?.message}
              {...register('email')}
            />
            <TextField
              label="Reset code"
              fullWidth
              autoComplete="one-time-code"
              error={!!errors.token}
              helperText={errors.token?.message ?? 'Copy the full code from the password reset email.'}
              {...register('token')}
            />
            <TextField
              label="New password"
              type={showNewPassword ? 'text' : 'password'}
              fullWidth
              autoComplete="new-password"
              error={!!errors.newPassword}
              helperText={errors.newPassword?.message}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showNewPassword ? 'Hide new password' : 'Show new password'}
                        aria-pressed={showNewPassword}
                        edge="end"
                        onMouseDown={(e) => e.preventDefault()}
                        onClick={() => setShowNewPassword((shown) => !shown)}
                      >
                        {showNewPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
              {...register('newPassword')}
            />
            <TextField
              label="Confirm new password"
              type={showConfirmPassword ? 'text' : 'password'}
              fullWidth
              autoComplete="new-password"
              error={!!errors.confirmPassword}
              helperText={errors.confirmPassword?.message}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showConfirmPassword ? 'Hide confirmation password' : 'Show confirmation password'}
                        aria-pressed={showConfirmPassword}
                        edge="end"
                        onMouseDown={(e) => e.preventDefault()}
                        onClick={() => setShowConfirmPassword((shown) => !shown)}
                      >
                        {showConfirmPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
              {...register('confirmPassword')}
            />
            <Button type="submit" variant="contained" size="large" fullWidth disabled={isSubmitting}>
              {isSubmitting ? 'Resetting…' : 'Reset password'}
            </Button>
          </Stack>
        </Box>
      )}
      {!success && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 3, justifyContent: 'space-between' }}>
          <MuiLink component={RouterLink} to="/forgot-password" variant="body2">
            Request a new code
          </MuiLink>
          <MuiLink component={RouterLink} to="/login" variant="body2">
            Back to sign in
          </MuiLink>
        </Stack>
      )}
    </Paper>
  )
}
