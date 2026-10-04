import { useEffect, useState } from 'react'
import { Alert, Box, Button, Chip, CircularProgress, Stack, TextField, Typography } from '@mui/material'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { disableMfa, enableMfa, getMfaStatus, setupMfa, type MfaSetupResult } from '../api'

type Phase = 'loading' | 'idle' | 'enrolling' | 'disabling' | 'error'

export function MfaSection() {
  const { notify } = useSnackbar()
  const [phase, setPhase] = useState<Phase>('loading')
  const [enabled, setEnabled] = useState(false)
  const [setup, setSetup] = useState<MfaSetupResult | null>(null)
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [loadAttempt, setLoadAttempt] = useState(0)

  useEffect(() => {
    let active = true
    setPhase('loading')
    getMfaStatus()
      .then((s) => {
        if (!active) return
        setEnabled(s.mfaEnabled)
        setPhase('idle')
      })
      .catch(() => {
        if (active) setPhase('error')
      })
    return () => { active = false }
  }, [loadAttempt])

  async function handleStartEnroll() {
    setBusy(true)
    try {
      const result = await setupMfa()
      setSetup(result)
      setPhase('enrolling')
    } catch {
      notify('Failed to start MFA setup', 'error')
    } finally {
      setBusy(false)
    }
  }

  async function handleConfirmEnable() {
    if (!setup || code.length < 6) return
    setBusy(true)
    try {
      await enableMfa(setup.secret, code)
      setEnabled(true)
      setPhase('idle')
      setSetup(null)
      setCode('')
      notify('Two-factor authentication enabled', 'success')
    } catch {
      notify('Invalid code — please try again', 'error')
    } finally {
      setBusy(false)
    }
  }

  async function handleConfirmDisable() {
    if (code.length < 6) return
    setBusy(true)
    try {
      await disableMfa(code)
      setEnabled(false)
      setPhase('idle')
      setCode('')
      notify('Two-factor authentication disabled', 'success')
    } catch {
      notify('Invalid code — please try again', 'error')
    } finally {
      setBusy(false)
    }
  }

  if (phase === 'loading') {
    return <CircularProgress size={20} />
  }

  if (phase === 'error') {
    return (
      <Alert
        severity="error"
        action={<Button color="inherit" size="small" onClick={() => setLoadAttempt((attempt) => attempt + 1)}>Retry</Button>}
      >
        Two-factor authentication status is unavailable. Retry before changing this setting.
      </Alert>
    )
  }

  return (
    <Box>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Box>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Two-factor authentication
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Require a 6-digit authenticator code at sign-in.
          </Typography>
        </Box>
        <Chip size="small" label={enabled ? 'Enabled' : 'Disabled'} color={enabled ? 'success' : 'default'} />
      </Stack>

      {phase === 'idle' && (
        <Button
          size="small"
          variant="outlined"
          color={enabled ? 'error' : 'primary'}
          disabled={busy}
          onClick={() => (enabled ? setPhase('disabling') : void handleStartEnroll())}
        >
          {enabled ? 'Disable 2FA' : 'Enable 2FA'}
        </Button>
      )}

      {phase === 'enrolling' && setup && (
        <Stack spacing={1.5} sx={{ mt: 1 }}>
          <Typography variant="caption" color="text.secondary">
            Add this to your authenticator app (Google Authenticator, Authy, etc.), or enter the secret manually.
          </Typography>
          <TextField size="small" label="Setup URI" value={setup.setupUri} slotProps={{ input: { readOnly: true } }} fullWidth />
          <TextField size="small" label="Secret" value={setup.secret} slotProps={{ input: { readOnly: true } }} fullWidth />
          <TextField
            size="small"
            label="6-digit code"
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            sx={{ maxWidth: 160 }}
          />
          <Stack direction="row" spacing={1}>
            <Button size="small" variant="contained" disabled={busy || code.length < 6} onClick={() => void handleConfirmEnable()}>
              Confirm & enable
            </Button>
            <Button
              size="small"
              onClick={() => {
                setPhase('idle')
                setSetup(null)
                setCode('')
              }}
            >
              Cancel
            </Button>
          </Stack>
        </Stack>
      )}

      {phase === 'disabling' && (
        <Stack spacing={1.5} sx={{ mt: 1 }}>
          <Typography variant="caption" color="text.secondary">
            Enter a current 6-digit code to confirm disabling 2FA.
          </Typography>
          <TextField
            size="small"
            label="6-digit code"
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            sx={{ maxWidth: 160 }}
          />
          <Stack direction="row" spacing={1}>
            <Button size="small" color="error" variant="contained" disabled={busy || code.length < 6} onClick={() => void handleConfirmDisable()}>
              Confirm & disable
            </Button>
            <Button
              size="small"
              onClick={() => {
                setPhase('idle')
                setCode('')
              }}
            >
              Cancel
            </Button>
          </Stack>
        </Stack>
      )}
    </Box>
  )
}
