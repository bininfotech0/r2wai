import type { ReactNode } from 'react'
import { Box, Stack, Typography } from '@mui/material'

interface PageHeaderProps {
  title: string
  description?: string
  actions?: ReactNode
}

/**
 * The title/description/actions row every list and detail page renders at
 * the top — previously hand-rolled per page (Departments, Runs, Approvals,
 * ...). MainLayout already owns the breadcrumb trail above this, so this
 * component only covers the title row itself.
 */
export function PageHeader({ title, description, actions }: PageHeaderProps) {
  return (
    <Stack
      component="header"
      direction={{ xs: 'column', sm: 'row' }}
      spacing={{ xs: 1, sm: 2 }}
      sx={{ alignItems: { xs: 'stretch', sm: 'center' }, justifyContent: 'space-between', mb: { xs: 2, md: 3 } }}
    >
      <Box sx={{ minWidth: 0 }}>
        <Typography
          variant="h4"
          component="h1"
          sx={{ fontSize: { xs: '1.55rem', sm: '1.85rem', lg: '2.1rem' }, fontWeight: 750, letterSpacing: '-0.035em', lineHeight: 1.15 }}
        >
          {title}
        </Typography>
        {description && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.65, maxWidth: 760 }}>
            {description}
          </Typography>
        )}
      </Box>
      {actions && (
        <Stack direction="row" spacing={1} sx={{ flexShrink: 0, flexWrap: 'wrap', rowGap: 1, justifyContent: { xs: 'flex-start', sm: 'flex-end' }, '& .MuiButton-root': { minWidth: { sm: 112 } } }}>
          {actions}
        </Stack>
      )}
    </Stack>
  )
}
