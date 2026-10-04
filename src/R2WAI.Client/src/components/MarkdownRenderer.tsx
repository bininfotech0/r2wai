import { Box } from '@mui/material'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

interface MarkdownRendererProps {
  content: string
}

/** react-markdown + GFM (tables, strikethrough, task lists) — replaces Blazor's Markdig-based renderer. */
export function MarkdownRenderer({ content }: MarkdownRendererProps) {
  return (
    <Box
      sx={{
        fontSize: '0.875rem',
        lineHeight: 1.6,
        '& p': { m: 0, mb: 1, '&:last-child': { mb: 0 } },
        '& pre': {
          bgcolor: 'action.hover',
          p: 1.5,
          borderRadius: 1,
          overflowX: 'auto',
          fontSize: '0.8125rem',
        },
        '& code': { fontFamily: 'ui-monospace, monospace', fontSize: '0.85em' },
        '& :not(pre) > code': {
          bgcolor: 'action.hover',
          px: 0.5,
          py: 0.125,
          borderRadius: 0.5,
        },
        '& table': { borderCollapse: 'collapse', width: '100%', my: 1 },
        '& th, & td': { border: '1px solid', borderColor: 'divider', px: 1, py: 0.5 },
        '& ul, & ol': { pl: 3, my: 1 },
        '& a': { color: 'primary.main' },
      }}
    >
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
    </Box>
  )
}
