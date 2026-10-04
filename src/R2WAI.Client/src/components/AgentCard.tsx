import { useState, type ReactNode } from 'react'
import {
  Avatar,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  IconButton,
  Menu,
  MenuItem,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import { StatusBadge } from './StatusBadge'

/**
 * The assistant/agent tile used by the Assistants library, the dashboard's recent-agents
 * rail and the channel overview. Extracted from the library page's inline markup so those
 * three surfaces cannot drift apart in status wording, chip vocabulary or row height.
 *
 * `actions` are supplied by the caller rather than hardcoded here: publish/unpublish/
 * archive/delete exist as endpoints, but duplicate and rollback do not
 * (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.3), so this component will not offer a
 * control that cannot succeed.
 */
export interface AgentCardAction {
  label: string
  onSelect: () => void
  /** Renders the item in the error colour, e.g. Delete. */
  destructive?: boolean
  disabled?: boolean
}

interface AgentCardProps {
  name: string
  description: string | null | undefined
  /** Matches AssistantDto['publishStatus']: Draft | Published | Archived. */
  status: string
  /** Assistant type label, e.g. "HR". Optional — some surfaces have no type. */
  type?: string
  avatarUrl?: string | null
  /** AssistantDto.usageCount. Omit to hide the footer. */
  usageCount?: number
  /** AssistantDto.publishedVersion, shown as a chip when the assistant is published. */
  publishedVersion?: number
  /** True when a knowledge base is linked — the "grounded" signal at a glance. */
  hasKnowledge?: boolean
  /** Channel names this agent is live on. Rendered as a compact chip row. */
  channels?: string[]
  actions?: AgentCardAction[]
  onOpen: () => void
  /** Overrides the trailing menu trigger, e.g. to nest it in a parent CardActionArea. */
  footer?: ReactNode
}

export function AgentCard({
  name,
  description,
  status,
  type,
  avatarUrl,
  usageCount,
  publishedVersion,
  hasKnowledge,
  channels,
  actions,
  onOpen,
}: AgentCardProps) {
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)

  return (
    <Card variant="outlined" sx={{ height: '100%', display: 'flex', position: 'relative' }}>
      <CardActionArea
        onClick={onOpen}
        sx={{ flexGrow: 1, alignItems: 'stretch' }}
        aria-label={`Open ${name}`}
      >
        <CardContent sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1, pr: 3 }}>
            <Avatar src={avatarUrl ?? undefined} sx={{ width: 28, height: 28 }}>
              <SmartToyOutlined fontSize="small" />
            </Avatar>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, flexGrow: 1 }} noWrap title={name}>
              {name}
            </Typography>
          </Stack>

          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ mb: 1.5, flexGrow: 1, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}
          >
            {description || 'No description yet.'}
          </Typography>

          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 0.5 }}>
            <StatusBadge status={status} />
            {type && <Chip label={type} size="small" variant="outlined" />}
            {hasKnowledge && <Chip label="Knowledge" size="small" variant="outlined" />}
            {publishedVersion != null && publishedVersion > 0 && (
              <Chip label={`v${publishedVersion}`} size="small" variant="outlined" />
            )}
            {channels?.map((channel) => (
              <Chip key={channel} label={channel} size="small" variant="outlined" />
            ))}
          </Stack>

          {usageCount !== undefined && (
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>
              {status === 'Published' ? `Used ${usageCount} times` : 'Not published'}
            </Typography>
          )}
        </CardContent>
      </CardActionArea>

      {actions && actions.length > 0 && (
        <>
          <Tooltip title={`Actions for ${name}`}>
            <IconButton
              size="small"
              aria-label={`Actions for ${name}`}
              onClick={(e) => {
                e.stopPropagation()
                setMenuAnchor(e.currentTarget)
              }}
              sx={{ position: 'absolute', top: 8, right: 8 }}
            >
              <MoreVertIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)}>
            {actions.map((action) => (
              <MenuItem
                key={action.label}
                disabled={action.disabled}
                onClick={() => {
                  setMenuAnchor(null)
                  action.onSelect()
                }}
                sx={action.destructive ? { color: 'error.main' } : undefined}
              >
                {action.label}
              </MenuItem>
            ))}
          </Menu>
        </>
      )}
    </Card>
  )
}
