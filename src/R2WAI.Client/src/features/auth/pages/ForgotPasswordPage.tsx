import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Alert, Box, Button, Link as MuiLink, Paper, Stack, TextField, Typography } from '@mui/material'
import { Link as RouterLink, useNavigate } from 'react-router-dom'
import { forgotPasswordRequest } from '../../../lib/auth/authClient'
import { useDocumentTitle } from '../../../lib/useDocumentTitle'

const schema = z.object({
  email: z.string().email('Enter a valid email address'),
})

type FormValues = z.infer<typeof schema>

export function ForgotPasswordPage() {
  useDocumentTitle('Forgot Password · R2WAI Studio')
  const navigate = useNavigate()
  const [submitted, setSubmitted] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  async function onSubmit(values: FormValues) {
    setError(null)
    setIsSubmitting(true)
    try {
      await forgotPasswordRequest(values.email)
      setSubmitted(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to send reset instructions.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Paper elevation={0} sx={{ p: 4, width: 400, maxWidth: '90vw' }}>
      <Typography variant="h6" sx={{ mb: 0.5, fontWeight: 700 }}>
        Reset your password
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Enter your email and we'll send you reset instructions.
      </Typography>

      {submitted ? (
        <Stack spacing={2}>
          <Alert severity="success">
            If an account exists for that email, reset instructions have been sent. Check your inbox and spam folder; delivery may take a few minutes.
          </Alert>
          <Button
            variant="contained"
            size="large"
            fullWidth
            onClick={() => navigate('/reset-password', { state: { email: getValues('email') } })}
          >
            Enter reset code
          </Button>
          <Button variant="outlined" fullWidth onClick={() => setSubmitted(false)}>
            Use a different email address
          </Button>
        </Stack>
      ) : (
        <Box component="form" onSubmit={(e) => void handleSubmit(onSubmit)(e)} noValidate>
          <Stack spacing={2}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label="Email"
              type="email"
              fullWidth
              autoFocus
              autoComplete="email"
              inputMode="email"
              error={!!errors.email}
              helperText={errors.email?.message}
              {...register('email')}
            />
            <Button type="submit" variant="contained" size="large" fullWidth disabled={isSubmitting}>
              {isSubmitting ? 'Sending…' : 'Send reset instructions'}
            </Button>
          </Stack>
        </Box>
      )}

      <Typography variant="body2" align="center" sx={{ mt: 3 }}>
        <MuiLink component={RouterLink} to="/login">
          Back to sign in
        </MuiLink>
      </Typography>
    </Paper>
  )
}
