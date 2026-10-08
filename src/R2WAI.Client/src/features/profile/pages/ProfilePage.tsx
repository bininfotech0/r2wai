import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import {
  Alert,
  Avatar,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import PersonOutlineIcon from '@mui/icons-material/PersonOutlineOutlined'
import ShieldOutlinedIcon from '@mui/icons-material/ShieldOutlined'
import { useAuth } from '../../../lib/auth/useAuth'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { authFetch, forgotPasswordRequest } from '../../../lib/auth/authClient'
import { getPersona, getPersonaLabel } from '../../../lib/nav/roleNav'
import { changePassword, updateProfile, uploadAvatar, type UpdateProfileInput } from '../api'
import { MfaSection } from '../components/MfaSection'
import { ApiRequestError } from '../../../lib/api/fetchJson'

function useAuthenticatedImage(url: string | null) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!url) {
      setObjectUrl(null)
      return
    }
    let revoked: string | null = null
    let cancelled = false
    void authFetch(url)
      // Discard a failed response's body (e.g. 404 for an avatar whose file is gone) so the request
      // actually completes instead of staying open.
      .then((res) => (res.ok ? res.blob() : res.body?.cancel().then(() => null) ?? null))
      .then((blob) => {
        if (cancelled || !blob) return
        const created = URL.createObjectURL(blob)
        revoked = created
        setObjectUrl(created)
      })
      .catch(() => {})
    return () => {
      cancelled = true
      if (revoked) URL.revokeObjectURL(revoked)
    }
  }, [url])

  return objectUrl
}

export function ProfilePage() {
  const { user, refreshUser } = useAuth()
  const { notify } = useSnackbar()
  const persona = getPersona(user)

  const [editOpen, setEditOpen] = useState(false)
  const [form, setForm] = useState<UpdateProfileInput>({
    firstName: user?.firstName ?? '',
    lastName: user?.lastName ?? '',
    mobileNumber: user?.mobileNumber ?? '',
    email: user?.email ?? '',
  })
  const [saving, setSaving] = useState(false)
  const [resetSending, setResetSending] = useState(false)
  const [pwOpen, setPwOpen] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [pwError, setPwError] = useState<string | null>(null)
  const [pwSaving, setPwSaving] = useState(false)
  const [avatarUploading, setAvatarUploading] = useState(false)
  const avatarInputRef = useRef<HTMLInputElement>(null)
  const avatarPreviewUrl = useAuthenticatedImage(user?.avatarUrl ?? null)

  if (!user) return null

  async function handleAvatarSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    setAvatarUploading(true)
    try {
      await uploadAvatar(file)
      await refreshUser()
      notify('Avatar updated', 'success')
    } catch (err) {
      notify(err instanceof ApiRequestError ? err.message : 'Failed to upload avatar', 'error')
    } finally {
      setAvatarUploading(false)
    }
  }

  function openEdit() {
    setForm({
      firstName: user!.firstName,
      lastName: user!.lastName,
      mobileNumber: user!.mobileNumber ?? '',
      email: user!.email ?? '',
    })
    setEditOpen(true)
  }

  async function handleSave() {
    setSaving(true)
    try {
      await updateProfile({
        firstName: form.firstName,
        lastName: form.lastName,
        mobileNumber: form.mobileNumber || null,
        email: form.email || null,
      })
      await refreshUser()
      notify('Profile updated', 'success')
      setEditOpen(false)
    } catch {
      notify('Failed to update profile', 'error')
    } finally {
      setSaving(false)
    }
  }

  function openChangePassword() {
    setCurrentPassword('')
    setNewPassword('')
    setConfirmPassword('')
    setPwError(null)
    setPwOpen(true)
  }

  async function handleChangePassword() {
    if (newPassword !== confirmPassword) {
      setPwError('New passwords do not match.')
      return
    }
    setPwSaving(true)
    setPwError(null)
    try {
      await changePassword(currentPassword, newPassword)
      notify('Password changed', 'success')
      setPwOpen(false)
    } catch (err) {
      setPwError(err instanceof ApiRequestError ? err.message : 'Failed to change password')
    } finally {
      setPwSaving(false)
    }
  }

  async function handleSendResetLink() {
    if (!user?.email) return
    setResetSending(true)
    try {
      await forgotPasswordRequest(user.email)
      notify('Password reset link sent to your email', 'success')
    } catch {
      notify('Failed to send reset link', 'error')
    } finally {
      setResetSending(false)
    }
  }

  return (
    <Box>
      <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.5 }}>
        Profile
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Your account details and security settings.
      </Typography>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 5 }}>
          <Paper variant="outlined" sx={{ p: 3 }}>
            <Stack spacing={1.5} sx={{ alignItems: 'center', mb: 2 }}>
              <Box sx={{ position: 'relative' }}>
                <Avatar src={avatarPreviewUrl ?? undefined} sx={{ width: 72, height: 72, fontSize: '1.75rem' }}>
                  {(user.displayName || user.email || 'U').charAt(0).toUpperCase()}
                </Avatar>
                {avatarUploading && (
                  <CircularProgress
                    size={72}
                    sx={{ position: 'absolute', top: 0, left: 0 }}
                  />
                )}
              </Box>
              <Box sx={{ textAlign: 'center' }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                  {user.displayName}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {user.email ?? 'No email on file'}
                </Typography>
              </Box>
              <Chip size="small" label={getPersonaLabel(persona)} icon={<PersonOutlineIcon />} />
              <input
                ref={avatarInputRef}
                type="file"
                accept="image/png,image/jpeg,image/webp,image/gif"
                hidden
                onChange={(e) => void handleAvatarSelected(e)}
              />
              <Button size="small" disabled={avatarUploading} onClick={() => avatarInputRef.current?.click()}>
                {avatarUploading ? 'Uploading…' : 'Change photo'}
              </Button>
            </Stack>
            <Button fullWidth variant="contained" onClick={openEdit}>
              Edit profile
            </Button>
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, md: 7 }}>
          <Paper variant="outlined" sx={{ p: 3, mb: 2 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>
              Account details
            </Typography>
            <Stack spacing={1.25}>
              <DetailRow label="Mobile number" value={user.mobileNumber ?? '—'} />
              <DetailRow label="Roles" value={user.roles.join(', ') || '—'} />
              <DetailRow label="Last login" value={user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : '—'} />
              <DetailRow label="Member since" value={new Date(user.createdAt).toLocaleDateString()} />
            </Stack>
          </Paper>

          <Paper variant="outlined" sx={{ p: 3 }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1.5 }}>
              <ShieldOutlinedIcon fontSize="small" color="primary" />
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Security
              </Typography>
            </Stack>

            <Stack spacing={2}>
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
                  Password
                </Typography>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'center' } }}>
                  <Button size="small" variant="outlined" onClick={openChangePassword}>
                    Change password
                  </Button>
                  <Button size="small" disabled={!user.email || resetSending} onClick={() => void handleSendResetLink()}>
                    {resetSending ? 'Sending…' : 'Email me a reset link instead'}
                  </Button>
                </Stack>
              </Box>

              <Divider />

              <MfaSection />
            </Stack>
          </Paper>
        </Grid>
      </Grid>

      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Edit profile</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="First name"
              value={form.firstName}
              onChange={(e) => setForm((f) => ({ ...f, firstName: e.target.value }))}
              fullWidth
            />
            <TextField
              label="Last name"
              value={form.lastName}
              onChange={(e) => setForm((f) => ({ ...f, lastName: e.target.value }))}
              fullWidth
            />
            <TextField
              label="Mobile number"
              value={form.mobileNumber ?? ''}
              onChange={(e) => setForm((f) => ({ ...f, mobileNumber: e.target.value }))}
              fullWidth
            />
            <TextField
              label="Email"
              value={form.email ?? ''}
              disabled={!!user.email}
              helperText={user.email ? "Email can't be changed once set." : 'Can only be set once.'}
              onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={saving} onClick={() => void handleSave()}>
            {saving ? 'Saving…' : 'Save changes'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={pwOpen} onClose={() => setPwOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Change password</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {pwError && <Alert severity="error">{pwError}</Alert>}
            <TextField
              label="Current password"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              fullWidth
            />
            <TextField
              label="New password"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              fullWidth
            />
            <TextField
              label="Confirm new password"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPwOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            disabled={pwSaving || !currentPassword || !newPassword || !confirmPassword}
            onClick={() => void handleChangePassword()}
          >
            {pwSaving ? 'Changing…' : 'Change password'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body2" sx={{ fontWeight: 500 }}>
        {value}
      </Typography>
    </Stack>
  )
}
