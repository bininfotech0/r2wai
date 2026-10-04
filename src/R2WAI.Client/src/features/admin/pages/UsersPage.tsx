import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import BadgeIcon from '@mui/icons-material/Badge'
import LockOpenIcon from '@mui/icons-material/LockOpen'
import PhonelinkEraseIcon from '@mui/icons-material/PhonelinkErase'
import DomainIcon from '@mui/icons-material/Domain'
import PeopleOutlinedIcon from '@mui/icons-material/PeopleOutlined'
import AdminPanelSettingsOutlinedIcon from '@mui/icons-material/AdminPanelSettingsOutlined'
import Tooltip from '@mui/material/Tooltip'
import type { GridColDef } from '@mui/x-data-grid'
import { DataTable } from '../../../components/data/DataTable'
import { TooltipIconButton } from '../../../components/TooltipIconButton'
import { EmptyState } from '../../../components/EmptyState'
import { PageHeader } from '../../../components/PageHeader'
import { StatusBadge } from '../../../components/StatusBadge'
import { ConfirmDeleteDialog } from '../../../components/dialogs/ConfirmDeleteDialog'
import { useSnackbar } from '../../../lib/notifications/useSnackbar'
import { useAuth } from '../../../lib/auth/useAuth'
import { getPersona } from '../../../lib/nav/roleNav'
import {
  assignUserRoles,
  createRole,
  createTenant,
  createUser,
  deleteRole,
  deleteTenant,
  deleteUser,
  inviteUser,
  listRoles,
  listRolesPage,
  listTenants,
  listUsers,
  resetUserMfa,
  unlockUser,
  updateRole,
  updateTenant,
  updateUser,
} from '../api'
import { AssignRolesDialog } from '../dialogs/AssignRolesDialog'
import { CreateEditRoleDialog, type RoleFormValues } from '../dialogs/CreateEditRoleDialog'
import { CreateEditTenantDialog, type TenantFormValues } from '../dialogs/CreateEditTenantDialog'
import { CreateEditUserDialog } from '../dialogs/CreateEditUserDialog'
import type { CreateUserInput, RoleDto, TenantDto, UpdateUserInput, UserDto } from '../types'

// Mirrors roleNav.ts's getPersona() exactly — the RBAC collapse migration
// (20260829091112_CollapseRbacToThreeRoles) means these 3 system roles are now the *complete*
// real role set, not a subset mapped down from a larger one. A custom (non-system) tenant role
// shares no relationship with nav visibility at all — it's a pure authorization role.
const NAV_PERSONA_BY_ROLE: Record<string, string> = {
  SystemAdmin: 'Super Admin',
  Admin: 'Admin',
  User: 'User',
}

export function UsersPage() {
  const { user } = useAuth()
  const isSuperAdmin = getPersona(user) === 'SuperAdmin'

  const [tab, setTab] = useState<'Users' | 'Roles' | 'Organizations'>('Users')
  const queryClient = useQueryClient()
  const { notify } = useSnackbar()

  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [rolePage, setRolePage] = useState(1)
  const [rolePageSize, setRolePageSize] = useState(20)
  const [userDialogOpen, setUserDialogOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<UserDto | null>(null)
  const [deletingUser, setDeletingUser] = useState<UserDto | null>(null)
  const [rolesTarget, setRolesTarget] = useState<UserDto | null>(null)

  const [roleDialogOpen, setRoleDialogOpen] = useState(false)
  const [editingRole, setEditingRole] = useState<RoleDto | null>(null)
  const [deletingRole, setDeletingRole] = useState<RoleDto | null>(null)

  const [tenantPage, setTenantPage] = useState(1)
  const [tenantPageSize, setTenantPageSize] = useState(20)
  const [tenantSearch, setTenantSearch] = useState('')
  const [debouncedTenantSearch, setDebouncedTenantSearch] = useState('')
  const [tenantDialogOpen, setTenantDialogOpen] = useState(false)
  const [editingTenant, setEditingTenant] = useState<TenantDto | null>(null)
  const [deletingTenant, setDeletingTenant] = useState<TenantDto | null>(null)

  const [inviteOpen, setInviteOpen] = useState(false)
  const [inviteEmail, setInviteEmail] = useState('')

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search.trim()), 300)
    return () => window.clearTimeout(timeout)
  }, [search])

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedTenantSearch(tenantSearch.trim()), 300)
    return () => window.clearTimeout(timeout)
  }, [tenantSearch])

  const usersQuery = useQuery({
    queryKey: ['admin-users', page, debouncedSearch],
    queryFn: () => listUsers(page, 20, debouncedSearch),
  })
  const rolesQuery = useQuery({ queryKey: ['admin-roles'], queryFn: listRoles })
  const roles = rolesQuery.data?.items ?? []
  const rolesPageQuery = useQuery({
    queryKey: ['admin-role-pages', rolePage, rolePageSize],
    queryFn: () => listRolesPage(rolePage, rolePageSize),
    enabled: tab === 'Roles',
  })
  const tenantsQuery = useQuery({
    queryKey: ['admin-tenants', tenantPage, tenantPageSize, debouncedTenantSearch],
    queryFn: () => listTenants(tenantPage, tenantPageSize, debouncedTenantSearch),
    enabled: isSuperAdmin && tab === 'Organizations',
  })

  const invalidateUsers = () => queryClient.invalidateQueries({ queryKey: ['admin-users'] })
  const invalidateRoles = () => {
    void queryClient.invalidateQueries({ queryKey: ['admin-roles'] })
    void queryClient.invalidateQueries({ queryKey: ['admin-role-pages'] })
  }

  const createUserMutation = useMutation({
    mutationFn: (v: CreateUserInput) => createUser(v),
    onSuccess: () => {
      notify('User created', 'success')
      setUserDialogOpen(false)
      void invalidateUsers()
    },
    onError: () => notify('Failed to create user', 'error'),
  })
  const updateUserMutation = useMutation({
    mutationFn: (input: { id: string; values: UpdateUserInput }) => updateUser(input.id, input.values),
    onSuccess: () => {
      notify('User updated', 'success')
      setUserDialogOpen(false)
      setEditingUser(null)
      void invalidateUsers()
    },
    onError: () => notify('Failed to update user', 'error'),
  })
  const deleteUserMutation = useMutation({
    mutationFn: deleteUser,
    onSuccess: () => {
      notify('User deleted', 'success')
      setDeletingUser(null)
      void invalidateUsers()
    },
    onError: () => notify('Failed to delete user', 'error'),
  })
  const assignRolesMutation = useMutation({
    mutationFn: (input: { id: string; roleIds: string[] }) => assignUserRoles(input.id, input.roleIds),
    onSuccess: () => {
      notify('Roles updated', 'success')
      setRolesTarget(null)
      void invalidateUsers()
    },
    onError: () => notify('Failed to update roles', 'error'),
  })
  const inviteMutation = useMutation({
    mutationFn: inviteUser,
    onSuccess: () => {
      notify('Invitation sent', 'success')
      setInviteOpen(false)
      setInviteEmail('')
      void invalidateUsers()
    },
    onError: () => notify('Failed to send invitation', 'error'),
  })
  // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.4 #70 — account-recovery actions. Fire immediately
  // like the Roles button above rather than behind a confirm dialog: both are recoverable
  // (a reset user just re-enrolls MFA next login; an unlock only matters if they were actually
  // locked out), unlike Delete, which is the one destructive action on this page.
  const resetMfaMutation = useMutation({
    mutationFn: resetUserMfa,
    onSuccess: (result) => notify(result.message, 'success'),
    onError: () => notify('Failed to reset MFA', 'error'),
  })
  const unlockMutation = useMutation({
    mutationFn: unlockUser,
    onSuccess: (result) => notify(result.message, 'success'),
    onError: () => notify('Failed to unlock account', 'error'),
  })

  const createRoleMutation = useMutation({
    mutationFn: createRole,
    onSuccess: () => {
      notify('Role created', 'success')
      setRoleDialogOpen(false)
      void invalidateRoles()
    },
    onError: () => notify('Failed to create role', 'error'),
  })
  const updateRoleMutation = useMutation({
    mutationFn: (input: { id: string; values: RoleFormValues }) => updateRole(input.id, input.values),
    onSuccess: () => {
      notify('Role updated', 'success')
      setRoleDialogOpen(false)
      setEditingRole(null)
      void invalidateRoles()
    },
    onError: () => notify('Failed to update role', 'error'),
  })
  const deleteRoleMutation = useMutation({
    mutationFn: deleteRole,
    onSuccess: () => {
      notify('Role deleted', 'success')
      setDeletingRole(null)
      const remainingCount = Math.max(0, (rolesPageQuery.data?.totalCount ?? 1) - 1)
      if (rolePage > 1 && (rolePage - 1) * rolePageSize >= remainingCount) setRolePage(rolePage - 1)
      void invalidateRoles()
    },
    onError: () => notify('Failed to delete role', 'error'),
  })

  const invalidateTenants = () => queryClient.invalidateQueries({ queryKey: ['admin-tenants'] })
  const createTenantMutation = useMutation({
    mutationFn: createTenant,
    onSuccess: () => {
      notify('Organisation created', 'success')
      setTenantDialogOpen(false)
      setTenantPage(1)
      void invalidateTenants()
    },
    onError: (error) => notify(error instanceof Error ? error.message : 'Failed to create organisation', 'error'),
  })
  const updateTenantMutation = useMutation({
    mutationFn: (input: { id: string; values: TenantFormValues }) => updateTenant(input.id, input.values),
    onSuccess: () => {
      notify('Organisation updated', 'success')
      setTenantDialogOpen(false)
      setEditingTenant(null)
      void invalidateTenants()
    },
    onError: (error) => notify(error instanceof Error ? error.message : 'Failed to update organisation', 'error'),
  })
  const deleteTenantMutation = useMutation({
    mutationFn: deleteTenant,
    onSuccess: () => {
      notify('Organisation suspended and removed', 'success')
      setDeletingTenant(null)
      const remainingCount = Math.max(0, (tenantsQuery.data?.totalCount ?? 1) - 1)
      if (tenantPage > 1 && (tenantPage - 1) * tenantPageSize >= remainingCount) setTenantPage(tenantPage - 1)
      void invalidateTenants()
    },
    onError: (error) => notify(error instanceof Error ? error.message : 'Failed to delete organisation', 'error'),
  })

  const userColumns: GridColDef<UserDto>[] = [
    { field: 'fullName', headerName: 'Name', flex: 1 },
    { field: 'email', headerName: 'Email', flex: 1 },
    {
      field: 'roles',
      headerName: 'Roles',
      flex: 1,
      renderCell: (params) => (
        <Stack direction="row" spacing={0.5}>
          {(params.value as string[]).map((r) => (
            <Chip key={r} label={r} size="small" variant="outlined" />
          ))}
        </Stack>
      ),
    },
    {
      field: 'lastLoginAt',
      headerName: 'Last Login',
      width: 170,
      valueFormatter: (v: string | null) => (v ? new Date(v).toLocaleString() : 'Never'),
    },
    {
      field: 'actions',
      headerName: '',
      width: 230,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <Tooltip title="Manage roles">
            <IconButton size="small" aria-label="Manage roles" onClick={(e) => { e.stopPropagation(); setRolesTarget(params.row) }}>
              <BadgeIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Reset MFA">
            <span>
              <IconButton
                size="small"
                aria-label="Reset MFA"
                disabled={resetMfaMutation.isPending}
                onClick={(e) => { e.stopPropagation(); resetMfaMutation.mutate(params.row.id) }}
              >
                <PhonelinkEraseIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title="Unlock account">
            <span>
              <IconButton
                size="small"
                aria-label="Unlock account"
                disabled={unlockMutation.isPending}
                onClick={(e) => { e.stopPropagation(); unlockMutation.mutate(params.row.id) }}
              >
                <LockOpenIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title="Edit user">
            <IconButton size="small" aria-label="Edit user" onClick={(e) => { e.stopPropagation(); setEditingUser(params.row); setUserDialogOpen(true) }}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Delete user">
            <IconButton size="small" aria-label="Delete user" onClick={(e) => { e.stopPropagation(); setDeletingUser(params.row) }}>
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
      ),
    },
  ]

  const roleColumns: GridColDef<RoleDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'description', headerName: 'Description', flex: 1.5 },
    {
      field: 'isSystem',
      headerName: 'System',
      width: 100,
      renderCell: (params) => (params.value ? <Chip label="System" size="small" variant="outlined" /> : null),
    },
    {
      field: 'navPersona',
      headerName: 'Nav Persona',
      width: 140,
      sortable: false,
      valueGetter: (_value, row: RoleDto) => NAV_PERSONA_BY_ROLE[row.name] ?? null,
      renderCell: (params) =>
        params.value ? (
          <Chip label={params.value} size="small" color="primary" variant="outlined" />
        ) : (
          <Typography variant="caption" color="text.secondary">
            Not tied to nav
          </Typography>
        ),
    },
    {
      field: 'actions',
      headerName: '',
      width: 90,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <Tooltip title="Edit role">
            <IconButton size="small" aria-label="Edit role" onClick={(e) => { e.stopPropagation(); setEditingRole(params.row); setRoleDialogOpen(true) }}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          {!params.row.isSystem && (
            <Tooltip title="Delete role">
              <IconButton size="small" aria-label="Delete role" onClick={(e) => { e.stopPropagation(); setDeletingRole(params.row) }}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Stack>
      ),
    },
  ]

  const tenantColumns: GridColDef<TenantDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1 },
    { field: 'slug', headerName: 'Slug', flex: 1 },
    { field: 'domain', headerName: 'Domain', flex: 1, valueGetter: (v: string | null) => v ?? '—' },
    {
      field: 'status',
      headerName: 'Status',
      width: 120,
      renderCell: (params) => <StatusBadge status={params.value} />,
    },
    { field: 'createdAt', headerName: 'Created', width: 150, valueFormatter: (v: string) => new Date(v).toLocaleDateString() },
    {
      field: 'actions',
      headerName: '',
      width: 90,
      sortable: false,
      renderCell: (params) => (
        <Stack direction="row">
          <TooltipIconButton size="small" aria-label="Edit organisation" onClick={(e) => { e.stopPropagation(); setEditingTenant(params.row); setTenantDialogOpen(true) }}>
            <EditIcon fontSize="small" />
          </TooltipIconButton>
          <TooltipIconButton size="small" aria-label="Delete organisation" onClick={(e) => { e.stopPropagation(); setDeletingTenant(params.row) }}>
            <DeleteIcon fontSize="small" />
          </TooltipIconButton>
        </Stack>
      ),
    },
  ]

  return (
    <Box>
      <PageHeader
        title="Users & Roles"
        description="Manage accounts, roles, and organization access."
        actions={
          tab === 'Users' ? (
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditingUser(null); setUserDialogOpen(true) }}>
              New User
            </Button>
          ) : tab === 'Roles' ? (
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditingRole(null); setRoleDialogOpen(true) }}>
              New Role
            </Button>
          ) : (
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditingTenant(null); setTenantDialogOpen(true) }}>
              New Organisation
            </Button>
          )
        }
      />

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Tab label="Users" value="Users" icon={<PeopleOutlinedIcon fontSize="small" />} iconPosition="start" />
        <Tab label="Roles" value="Roles" icon={<AdminPanelSettingsOutlinedIcon fontSize="small" />} iconPosition="start" />
        {isSuperAdmin && <Tab label="Organizations" value="Organizations" icon={<DomainIcon fontSize="small" />} iconPosition="start" />}
      </Tabs>

      {tab === 'Users' && (
        <Box>
          <Stack direction="row" spacing={1} sx={{ mb: 1.5, alignItems: 'center' }}>
            <Button
              size="small"
              startIcon={<PersonAddIcon />}
              onClick={() => setInviteOpen(true)}
              disabled={inviteMutation.isPending}
            >
              Invite by Email
            </Button>
          </Stack>
          <DataTable
            rows={usersQuery.data?.items ?? []}
            columns={userColumns}
            loading={usersQuery.isLoading}
            error={usersQuery.error}
            onRetry={() => void usersQuery.refetch()}
            rowCount={usersQuery.data?.totalCount ?? 0}
            page={page}
            pageSize={20}
            onPageChange={setPage}
            search={search}
            onSearchChange={(v) => { setSearch(v); setPage(1) }}
            searchPlaceholder="Search users…"
            emptyState={
              search.trim() ? (
                <EmptyState
                  title="No users match this search"
                  description="Try another name or email, or clear the search to see all users."
                  actionLabel="Clear search"
                  onAction={() => {
                    setSearch('')
                    setPage(1)
                  }}
                />
              ) : (
                <EmptyState
                  title="No users in this organization yet"
                  description="Create an account or invite a teammate by email."
                  actionLabel="Invite by email"
                  onAction={() => setInviteOpen(true)}
                />
              )
            }
          />
        </Box>
      )}

      {tab === 'Roles' && (
        <DataTable
          rows={rolesPageQuery.data?.items ?? []}
          columns={roleColumns}
          loading={rolesPageQuery.isLoading}
          error={rolesPageQuery.error}
          onRetry={() => void rolesPageQuery.refetch()}
          rowCount={rolesPageQuery.data?.totalCount ?? 0}
          page={rolePage}
          pageSize={rolePageSize}
          onPageChange={setRolePage}
          onPageSizeChange={(size) => {
            setRolePageSize(size)
            setRolePage(1)
          }}
          emptyState={
            <EmptyState
              title="No roles yet"
              description="Create a role to organize account access."
              actionLabel="Create role"
              onAction={() => {
                setEditingRole(null)
                setRoleDialogOpen(true)
              }}
            />
          }
        />
      )}

      {tab === 'Organizations' && isSuperAdmin && (
        <DataTable
          rows={tenantsQuery.data?.items ?? []}
          columns={tenantColumns}
          loading={tenantsQuery.isLoading}
          error={tenantsQuery.error}
          onRetry={() => void tenantsQuery.refetch()}
          rowCount={tenantsQuery.data?.totalCount ?? 0}
          page={tenantPage}
          pageSize={tenantPageSize}
          onPageChange={setTenantPage}
          onPageSizeChange={(size) => {
            setTenantPageSize(size)
            setTenantPage(1)
          }}
          search={tenantSearch}
          onSearchChange={(v) => { setTenantSearch(v); setTenantPage(1) }}
          searchPlaceholder="Search organisations…"
          emptyState={
            tenantSearch.trim() ? (
              <EmptyState
                title="No organisations match this search"
                description="Try another name or slug, or clear the search."
                actionLabel="Clear search"
                onAction={() => {
                  setTenantSearch('')
                  setTenantPage(1)
                }}
              />
            ) : (
              <EmptyState
                title="No organisations yet"
                description="Add an organisation to start managing a tenant."
                actionLabel="Create organisation"
                onAction={() => {
                  setEditingTenant(null)
                  setTenantDialogOpen(true)
                }}
              />
            )
          }
        />
      )}

      <CreateEditUserDialog
        open={userDialogOpen}
        onClose={() => { setUserDialogOpen(false); setEditingUser(null) }}
        onSubmit={(values) => {
          if (editingUser) {
            updateUserMutation.mutate({ id: editingUser.id, values: values as UpdateUserInput })
          } else {
            createUserMutation.mutate(values as CreateUserInput)
          }
        }}
        user={editingUser}
        isSubmitting={createUserMutation.isPending || updateUserMutation.isPending}
      />

      <AssignRolesDialog
        open={!!rolesTarget}
        onClose={() => setRolesTarget(null)}
        onSubmit={(roleIds) => {
          if (rolesTarget) assignRolesMutation.mutate({ id: rolesTarget.id, roleIds })
        }}
        user={rolesTarget}
        roles={roles}
        isSubmitting={assignRolesMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deletingUser}
        onClose={() => setDeletingUser(null)}
        onConfirm={() => deletingUser && deleteUserMutation.mutate(deletingUser.id)}
        title="Delete User"
        message={`Delete "${deletingUser?.fullName}"? This cannot be undone.`}
        isDeleting={deleteUserMutation.isPending}
      />

      <CreateEditRoleDialog
        open={roleDialogOpen}
        onClose={() => { setRoleDialogOpen(false); setEditingRole(null) }}
        onSubmit={(values) => {
          if (editingRole) {
            updateRoleMutation.mutate({ id: editingRole.id, values })
          } else {
            createRoleMutation.mutate(values)
          }
        }}
        role={editingRole}
        isSubmitting={createRoleMutation.isPending || updateRoleMutation.isPending}
      />

      <ConfirmDeleteDialog
        open={!!deletingRole}
        onClose={() => setDeletingRole(null)}
        onConfirm={() => deletingRole && deleteRoleMutation.mutate(deletingRole.id)}
        title="Delete Role"
        message={`Delete "${deletingRole?.name}"? This cannot be undone.`}
        isDeleting={deleteRoleMutation.isPending}
      />

      <Dialog open={inviteOpen} onClose={() => setInviteOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Invite by Email</DialogTitle>
        <Box
          component="form"
          onSubmit={(e) => {
            e.preventDefault()
            if (inviteEmail.trim()) inviteMutation.mutate(inviteEmail.trim())
          }}
        >
          <DialogContent>
            <TextField
              autoFocus
              label="Email address"
              type="email"
              autoComplete="email"
              inputMode="email"
              required
              fullWidth
              sx={{ mt: 1 }}
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
            />
          </DialogContent>
          <DialogActions sx={{ px: 3, pb: 2 }}>
            <Button onClick={() => setInviteOpen(false)} disabled={inviteMutation.isPending}>
              Cancel
            </Button>
            <Button
              type="submit"
              variant="contained"
              disabled={!inviteEmail.trim() || inviteMutation.isPending}
            >
              {inviteMutation.isPending ? 'Sending…' : 'Send Invite'}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      <CreateEditTenantDialog
        open={tenantDialogOpen}
        onClose={() => { setTenantDialogOpen(false); setEditingTenant(null) }}
        onSubmit={(values) => {
          if (editingTenant) {
            updateTenantMutation.mutate({ id: editingTenant.id, values })
          } else {
            createTenantMutation.mutate(values)
          }
        }}
        tenant={editingTenant}
        isSubmitting={createTenantMutation.isPending || updateTenantMutation.isPending}
        isOwnTenant={!!editingTenant && editingTenant.id === user?.tenantId}
      />

      <ConfirmDeleteDialog
        open={!!deletingTenant}
        onClose={() => setDeletingTenant(null)}
        onConfirm={() => deletingTenant && deleteTenantMutation.mutate(deletingTenant.id)}
        title="Delete Organisation"
        message={`Suspend and remove "${deletingTenant?.name}"? Its users will no longer be able to log in. This can be undone by a database administrator, but not from this page.`}
        isDeleting={deleteTenantMutation.isPending}
      />
    </Box>
  )
}
