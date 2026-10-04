import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  Alert,
  Box,
  Button,
  Divider,
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
import { useAuth } from '../../../lib/auth/useAuth'
import { AuthError } from '../../../lib/auth/types'
import { useDocumentTitle } from '../../../lib/useDocumentTitle'
import {
  changePasswordWithSetupToken,
  enableMfaWithSetupToken,
  setupMfaWithSetupToken,
} from '../../../lib/auth/authClient'

const schema = z.object({
  email: z.string().min(1, 'Email or Aadhaar number is required'),
  password: z.string().min(1, 'Password is required'),
  mfaCode: z.string().optional(),
})

type FormValues = z.infer<typeof schema>

export function LoginPage() {
  useDocumentTitle('Sign in · R2WAI Studio')
  const { login, loginWithEntra, isEntraAvailable } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const returnUrl = new URLSearchParams(location.search).get('returnUrl') ?? '/'

  const [mfaRequired, setMfaRequired] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [formNotice, setFormNotice] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isSsoLoading, setIsSsoLoading] = useState(false)
  const [showLoginPassword, setShowLoginPassword] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  // Set instead of a normal login result when a tenant's "Auth" GlobalPolicy blocks sign-in until
  // the user resolves something — see AuthController.Login's mfaSetupRequired/passwordChangeRequired.
  // Each carries the scoped setupToken the follow-up call needs (never stored in tokenStorage).
  const [mfaEnrollment, setMfaEnrollment] = useState<{ setupToken: string; secret: string; setupUri: string } | null>(null)
  const [mfaEnrollmentCode, setMfaEnrollmentCode] = useState('')
  const [passwordChange, setPasswordChange] = useState<{ setupToken: string; currentPassword: string } | null>(null)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [stepBusy, setStepBusy] = useState(false)

  // SSO's own MFA prompt — separate from the password path's `mfaRequired` above since it needs a
  // fresh Entra id_token on retry (handleSso re-runs loginMicrosoft()), not a resubmitted form field.
  const [ssoMfaRequired, setSsoMfaRequired] = useState(false)
  const [ssoMfaCode, setSsoMfaCode] = useState('')

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  async function onSubmit(values: FormValues) {
    setFormError(null)
    setFormNotice(null)
    setIsSubmitting(true)
    try {
      await login(values.email, values.password, values.mfaCode)
      navigate(returnUrl, { replace: true })
    } catch (err) {
      if (err instanceof AuthError) {
        setFormError(err.message)
        if (err.mfaRequired) setMfaRequired(true)
        if (err.mfaSetupRequired && err.setupToken) {
          try {
            const result = await setupMfaWithSetupToken(err.setupToken)
            setMfaEnrollment({ setupToken: err.setupToken, secret: result.secret, setupUri: result.setupUri })
          } catch {
            setFormError('Could not start MFA setup. Please try again.')
          }
        }
        if (err.passwordChangeRequired && err.setupToken) {
          setPasswordChange({ setupToken: err.setupToken, currentPassword: values.password })
        }
      } else {
        setFormError('Unable to connect to server.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleConfirmMfaEnrollment() {
    if (!mfaEnrollment || mfaEnrollmentCode.length < 6) return
    setFormError(null)
    setStepBusy(true)
    try {
      await enableMfaWithSetupToken(mfaEnrollment.setupToken, mfaEnrollment.secret, mfaEnrollmentCode)
      setMfaEnrollment(null)
      setMfaEnrollmentCode('')
      setMfaRequired(false)
      setFormNotice('MFA enabled. Sign in again with a code from your authenticator app.')
    } catch (err) {
      setFormError(err instanceof AuthError ? err.message : 'Invalid code — please try again.')
    } finally {
      setStepBusy(false)
    }
  }

  async function handleConfirmPasswordChange() {
    if (!passwordChange) return
    if (newPassword.length === 0 || newPassword !== confirmPassword) {
      setFormError('New password and confirmation must match.')
      return
    }
    setFormError(null)
    setStepBusy(true)
    try {
      await changePasswordWithSetupToken(passwordChange.setupToken, passwordChange.currentPassword, newPassword)
      setPasswordChange(null)
      setNewPassword('')
      setConfirmPassword('')
      setFormNotice('Password updated. Sign in again with your new password.')
    } catch (err) {
      setFormError(err instanceof AuthError ? err.message : 'Failed to set a new password.')
    } finally {
      setStepBusy(false)
    }
  }

  async function handleSso(mfaCode?: string) {
    setFormError(null)
    setIsSsoLoading(true)
    try {
      await loginWithEntra(mfaCode)
      setSsoMfaRequired(false)
      setSsoMfaCode('')
      navigate(returnUrl, { replace: true })
    } catch (err) {
      if (err instanceof AuthError && err.mfaRequired) {
        setSsoMfaRequired(true)
        setFormError(mfaCode ? 'Invalid code — please try again.' : null)
      } else {
        setFormError(err instanceof AuthError ? err.message : 'Microsoft sign-in failed.')
      }
    } finally {
      setIsSsoLoading(false)
    }
  }

  if (mfaEnrollment) {
    return (
      <Paper elevation={0} sx={{ p: 4, width: 400, maxWidth: '90vw' }}>
        <Stack spacing={0.5} sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Set up two-factor authentication
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Your organization requires MFA before you can sign in.
          </Typography>
        </Stack>
        {formError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}
        <Stack spacing={2}>
          <Typography variant="caption" color="text.secondary">
            Add this to your authenticator app (Google Authenticator, Authy, etc.), or enter the secret manually.
          </Typography>
          <TextField label="Setup URI" value={mfaEnrollment.setupUri} fullWidth slotProps={{ input: { readOnly: true } }} />
          <TextField label="Secret" value={mfaEnrollment.secret} fullWidth slotProps={{ input: { readOnly: true } }} />
          <TextField
            label="6-digit code"
            autoFocus
            inputMode="numeric"
            value={mfaEnrollmentCode}
            onChange={(e) => setMfaEnrollmentCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            slotProps={{ htmlInput: { maxLength: 6 } }}
          />
          <Button
            variant="contained"
            size="large"
            fullWidth
            disabled={stepBusy || mfaEnrollmentCode.length < 6}
            onClick={() => void handleConfirmMfaEnrollment()}
          >
            {stepBusy ? 'Verifying…' : 'Confirm & enable'}
          </Button>
        </Stack>
      </Paper>
    )
  }

  if (ssoMfaRequired) {
    return (
      <Paper elevation={0} sx={{ p: 4, width: 400, maxWidth: '90vw' }}>
        <Stack spacing={0.5} sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Two-factor authentication
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Enter the code from your authenticator app to finish signing in with Microsoft.
          </Typography>
        </Stack>
        {formError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}
        <Stack spacing={2}>
          <TextField
            label="Authenticator Code"
            placeholder="Enter 6-digit code"
            fullWidth
            autoFocus
            inputMode="numeric"
            value={ssoMfaCode}
            onChange={(e) => setSsoMfaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            slotProps={{ htmlInput: { maxLength: 6 } }}
          />
          <Button
            variant="contained"
            size="large"
            fullWidth
            disabled={isSsoLoading || ssoMfaCode.length < 6}
            onClick={() => void handleSso(ssoMfaCode)}
          >
            {isSsoLoading ? 'Verifying…' : 'Verify & sign in'}
          </Button>
          <Button
            size="small"
            onClick={() => {
              setSsoMfaRequired(false)
              setSsoMfaCode('')
              setFormError(null)
            }}
          >
            Cancel
          </Button>
        </Stack>
      </Paper>
    )
  }

  if (passwordChange) {
    return (
      <Paper elevation={0} sx={{ p: 4, width: 400, maxWidth: '90vw' }}>
        <Stack spacing={0.5} sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Set a new password
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Your password has expired and must be changed before you can sign in.
          </Typography>
        </Stack>
        {formError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}
        <Stack spacing={2}>
          <TextField
            label="New password"
            type={showNewPassword ? 'text' : 'password'}
            fullWidth
            autoFocus
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            autoComplete="new-password"
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
          />
          <TextField
            label="Confirm new password"
            type={showConfirmPassword ? 'text' : 'password'}
            fullWidth
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            autoComplete="new-password"
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
          />
          <Button
            variant="contained"
            size="large"
            fullWidth
            disabled={stepBusy || !newPassword}
            onClick={() => void handleConfirmPasswordChange()}
          >
            {stepBusy ? 'Saving…' : 'Set new password'}
          </Button>
        </Stack>
      </Paper>
    )
  }

  return (
    <Paper elevation={0} sx={{ p: 4, width: 400, maxWidth: '90vw' }}>
      <Stack spacing={0.5} sx={{ mb: 3 }}>
        <Typography variant="h6" sx={{ fontWeight: 700 }}>
          Sign in to your account
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Enter your credentials to continue
        </Typography>
      </Stack>

      {formError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {formError}
        </Alert>
      )}
      {formNotice && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {formNotice}
        </Alert>
      )}

      <Box component="form" onSubmit={(e) => void handleSubmit(onSubmit)(e)} noValidate>
        <Stack spacing={2}>
          <TextField
            label="Email or Aadhaar Number"
            fullWidth
            autoFocus
            autoComplete="username"
            error={!!errors.email}
            helperText={errors.email?.message ?? 'Use the email address or Aadhaar number on your account.'}
            {...register('email')}
          />
          <TextField
            label="Password"
            type={showLoginPassword ? 'text' : 'password'}
            fullWidth
            autoComplete="current-password"
            error={!!errors.password}
            helperText={errors.password?.message}
            slotProps={{
              input: {
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      aria-label={showLoginPassword ? 'Hide password' : 'Show password'}
                      aria-pressed={showLoginPassword}
                      edge="end"
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => setShowLoginPassword((shown) => !shown)}
                    >
                      {showLoginPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
                    </IconButton>
                  </InputAdornment>
                ),
              },
            }}
            {...register('password')}
          />
          {mfaRequired && (
            <TextField
              label="Authenticator Code"
              placeholder="Enter 6-digit code"
              fullWidth
              autoFocus
              inputMode="numeric"
              slotProps={{ htmlInput: { maxLength: 6 } }}
              {...register('mfaCode')}
            />
          )}

          <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
            <MuiLink component={RouterLink} to="/forgot-password" variant="caption">
              Forgot password?
            </MuiLink>
          </Box>

          <Button type="submit" variant="contained" size="large" fullWidth disabled={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
        </Stack>
      </Box>

      <Divider sx={{ my: 3 }}>
        <Typography variant="caption" color="text.secondary">
          or continue with
        </Typography>
      </Divider>

      <Button
        variant="outlined"
        size="large"
        fullWidth
        onClick={() => void handleSso()}
        disabled={isSsoLoading || !isEntraAvailable}
      >
        {isSsoLoading ? 'Signing in…' : 'Sign in with SSO'}
      </Button>
      {!isEntraAvailable && (
        <Typography variant="caption" color="text.secondary" align="center" sx={{ mt: 1, display: 'block' }}>
          Microsoft sign-in is not configured for this workspace.
        </Typography>
      )}

      <Typography variant="caption" color="text.secondary" align="center" sx={{ mt: 3, display: 'block' }}>
        Accounts are created by an administrator.
      </Typography>
    </Paper>
  )
}
