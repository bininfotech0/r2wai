import { IconButton as MuiIconButton, Tooltip } from '@mui/material'
import type { IconButtonProps } from '@mui/material/IconButton'

type TooltipTextProps =
  | { tooltip: string; 'aria-label'?: string }
  | { tooltip?: string; 'aria-label': string }

type TooltipIconButtonProps = Omit<IconButtonProps, 'aria-label'> & TooltipTextProps

/** Compact icon action with a consistent hover label and an accessible name. */
export function TooltipIconButton(props: TooltipIconButtonProps) {
  const { tooltip, ...buttonProps } = props
  const label = tooltip ?? buttonProps['aria-label']
  const button = (
    <MuiIconButton {...buttonProps} aria-label={buttonProps['aria-label'] ?? label}>
      {buttonProps.children}
    </MuiIconButton>
  )

  return (
    <Tooltip title={label} placement="top" enterDelay={450}>
      {buttonProps.disabled ? <span style={{ display: 'inline-flex' }}>{button}</span> : button}
    </Tooltip>
  )
}
