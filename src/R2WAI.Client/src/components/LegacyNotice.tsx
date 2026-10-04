import { Alert } from '@mui/material'

/**
 * Marks a screen that belongs to a retired part of the product. R2WAI is an AI assistant plus a
 * governed execution layer over existing enterprise apps, not a workflow product, so the
 * workflow-builder screens are kept out of primary navigation and labelled until the Elsa-backed
 * runtime they sit on is deleted.
 */
export function LegacyNotice({ feature }: { feature: string }) {
  return (
    <Alert severity="warning" variant="outlined" sx={{ mb: 2 }}>
      <strong>Legacy:</strong> {feature} will be removed. Use AI Assistants and Capabilities for new work.
    </Alert>
  )
}
