# Knowledge & RAG Standards

## Principle

RAG is a capability inside the Agent Runtime.

## Pipeline

```text
Source
↓
Extract
↓
OCR where needed
↓
Canonical Markdown
↓
Structure
↓
Chunk
↓
Metadata
↓
Embedding / Index
↓
Hybrid Retrieval
↓
Reranking
↓
Citation
```

## Sources

Priority support:
- PDF
- DOCX
- PPTX
- XLSX
- HTML
- TXT
- Markdown

Additional sources may be added through controlled providers.

## Original source

Always retain source identity needed for:
- citation
- page/section reference
- document status
- access control

## Markdown

Markdown is an internal canonical representation.

Users should not manually convert documents.

## Chunking

Chunking should preserve semantic boundaries where possible.

Consider:
- headings
- paragraphs
- tables
- lists
- page boundaries
- metadata

## Retrieval

Use appropriate combinations of:
- semantic search
- keyword/hybrid search
- metadata filters
- reranking

## Security

Knowledge retrieval must respect:
- tenant
- workspace
- source permissions
- agent permissions
- user permissions

Never return content solely because it is semantically relevant.

## Evaluation

Test:
- retrieval recall
- groundedness
- citation correctness
- permission filtering
- answer quality
