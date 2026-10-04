/** AssistantDto['publishStatus'] — Draft | Published | Archived. */
export const ASSISTANT_STATUSES = ['Draft', 'Published', 'Archived'] as const
export type AssistantStatusFilter = (typeof ASSISTANT_STATUSES)[number] | 'All'

export const ASSISTANT_SORTS = [
  { value: 'recent', label: 'Recently created' },
  { value: 'name', label: 'Name (A–Z)' },
  { value: 'usage', label: 'Most used' },
  { value: 'least-used', label: 'Least used' },
] as const
export type AssistantSortKey = (typeof ASSISTANT_SORTS)[number]['value']
