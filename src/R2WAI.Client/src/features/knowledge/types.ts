// Mirrors R2WAI.Application.Features.KnowledgeBases.DTOs.KnowledgeBaseDto (camelCase).
export interface KnowledgeBaseSourceDto {
  id: string
  type: string
  referenceId: string | null
  url: string | null
  content: string | null
  status: string | null
  chunkCount: number
  indexedAt: string | null
  error: string | null
  createdAt: string
}

export interface KnowledgeBaseDto {
  id: string
  applicationId: string | null
  name: string
  description: string | null
  status: string
  embeddingModel: string | null
  chunkSize: number | null
  chunkOverlap: number | null
  documentCount: number
  dataClassification: DataClassification
  createdAt: string
  sources: KnowledgeBaseSourceDto[]
}

// Mirrors R2WAI.Domain.Enums.DataClassification — same boundary already enforced for
// ModelConfiguration; here it gates whether this knowledge base's content may be pulled into RAG
// context under a tenant's "Knowledge" GlobalPolicy ceiling.
export type DataClassification = 'Public' | 'Internal' | 'Confidential' | 'Restricted'
export const DATA_CLASSIFICATIONS: DataClassification[] = ['Public', 'Internal', 'Confidential', 'Restricted']

export interface CreateKnowledgeBaseInput {
  name: string
  description?: string
  dataClassification?: DataClassification
}

export type UpdateKnowledgeBaseInput = CreateKnowledgeBaseInput

// Only `Type === "Url"` gets special backend handling (fetched + indexed
// server-side, see KnowledgeBaseService). Faq/Database/Integration are not
// separately parsed by the backend today — they index whatever Content text
// is supplied, same as a manual paste.
export type SourceType = 'Url' | 'Faq' | 'Database' | 'Integration'

export interface AddSourceInput {
  type: SourceType
  url?: string
  content?: string
}

// Mirrors R2WAI.Application.Features.Documents.DTOs.DocumentDto (camelCase).
export type DocumentType = 'PDF' | 'DOCX' | 'XLSX' | 'PPTX' | 'Image' | 'Text'
export type DocumentStatus = 'Uploading' | 'Processing' | 'Ready' | 'Failed'

export interface DocumentDto {
  id: string
  name: string
  description: string | null
  fileType: DocumentType
  fileSize: number
  status: DocumentStatus
  processingError: string | null
  pageCount: number | null
  knowledgeBaseId: string | null
  metadata: string | null
  createdAt: string
}

// There is no per-document/knowledge-base version history in the backend —
// POST /documents/compare instead compares two distinct documents' content
// via AI summarization. Repurposed here as "Compare Documents" rather than a
// literal version diff.
export interface ComparisonResultDto {
  comparison: string
  differences: string[] | null
  similarities: string[] | null
  similarityScore: number | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
