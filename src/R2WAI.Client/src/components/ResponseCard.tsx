import {
  Box,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import { StatusBadge } from './StatusBadge'
import type { ResponseCardData, ResponseCardField } from '../lib/chat/types'

interface ResponseCardProps {
  card: ResponseCardData
}

/**
 * Renders one structured chat response card (redesign plan Phase 3) — the shape a tool's result
 * takes instead of only being narrated in prose. Mirrors R2WAI.Application.Common.Models
 * .ResponseCardDto exactly; this is the single renderer for all four card types.
 */
export function ResponseCard({ card }: ResponseCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 1.5, mt: 1 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
        {card.title}
      </Typography>
      {(card.type === 'status' || card.type === 'summary') && card.fields && <FieldList fields={card.fields} />}
      {card.type === 'kpi' && card.fields && <KpiTiles fields={card.fields} />}
      {card.type === 'table' && card.columns && card.rows && <CardTable columns={card.columns} rows={card.rows} />}
    </Paper>
  )
}

function FieldList({ fields }: { fields: ResponseCardField[] }) {
  return (
    <Stack spacing={0.5}>
      {fields.map((f) => (
        <Stack key={f.label} direction="row" spacing={1} sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            {f.label}
          </Typography>
          {f.label === 'Status' ? (
            <StatusBadge status={f.value} />
          ) : (
            <Typography variant="body2" sx={{ fontWeight: 500 }}>
              {f.value}
            </Typography>
          )}
        </Stack>
      ))}
    </Stack>
  )
}

function KpiTiles({ fields }: { fields: ResponseCardField[] }) {
  return (
    <Stack direction="row" spacing={1.5} sx={{ flexWrap: 'wrap' }}>
      {fields.map((f) => (
        <Box key={f.label} sx={{ minWidth: 88 }}>
          <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
            {f.value}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {f.label}
          </Typography>
        </Box>
      ))}
    </Stack>
  )
}

function CardTable({ columns, rows }: { columns: string[]; rows: string[][] }) {
  return (
    <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            {columns.map((c) => (
              <TableCell key={c} sx={{ fontWeight: 600 }}>
                {c}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={columns.length}>
                <Typography variant="body2" color="text.secondary">
                  Nothing to show.
                </Typography>
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row, i) => (
              <TableRow key={i}>
                {row.map((cell, j) => (
                  <TableCell key={j}>{cell}</TableCell>
                ))}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  )
}
