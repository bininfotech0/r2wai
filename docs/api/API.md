# R2WAI API Reference

## Base URL

| Environment | URL |
|---|---|
| Local Development | `http://localhost:5000` |
| Staging | `https://staging.r2wai.com` |
| Production | `https://app.r2wai.com` |

## Authentication

### JWT Bearer Token

All API requests (except auth endpoints) require a Bearer token in the `Authorization` header:

```
Authorization: Bearer <token>
```

### Obtain Token

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "********"
}
```

Response:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "expiresAt": "2026-06-12T00:00:00Z",
  "user": {
    "id": "uuid",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "tenantId": "uuid"
  }
}
```

### Refresh Token

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "dGhpcyBpcyBh..."
}
```

### Entra ID (Azure AD)

```http
POST /api/v1/auth/entra-id
Content-Type: application/json

{
  "idToken": "...",
  "accessToken": "..."
}
```

## Common Headers

| Header | Required | Description |
|---|---|---|
| `Authorization` | Yes (except auth) | `Bearer <jwt-token>` |
| `X-Tenant-Id` | No | Explicit tenant override (admin use) |
| `X-Request-Id` | No | Idempotency key / correlation ID |
| `Accept-Language` | No | Locale for error messages |

## Rate Limiting

- **Authenticated**: 100 requests per minute per user
- **Anonymous**: 20 requests per minute per IP
- **Chat streaming**: 10 concurrent streams per user
- **Document upload**: 50 MB per file, 10 files per minute

Rate limit headers returned on every response:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1686547200
```

On rate limit exceeded, a `429 Too Many Requests` response is returned.

## Error Handling

All errors follow a consistent JSON envelope:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "A human-readable description of the error.",
    "details": [
      {
        "field": "email",
        "message": "'Email' is not a valid email address.",
        "code": "INVALID_FORMAT"
      }
    ],
    "traceId": "00-0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d-6a7b8c9d0e1f2a3b-01",
    "instance": "/api/v1/chat/conversations"
  }
}
```

### HTTP Status Codes

| Code | Meaning |
|---|---|
| `200` | Success |
| `201` | Created |
| `204` | No Content (successful delete) |
| `400` | Bad Request — validation error |
| `401` | Unauthorized — missing or invalid token |
| `403` | Forbidden — insufficient permissions |
| `404` | Not Found |
| `409` | Conflict — duplicate or state conflict |
| `422` | Unprocessable Entity |
| `429` | Too Many Requests — rate limit exceeded |
| `500` | Internal Server Error |

### Error Codes

| Code | Description |
|---|---|
| `VALIDATION_ERROR` | Request validation failed |
| `AUTHENTICATION_FAILED` | Invalid credentials |
| `TOKEN_EXPIRED` | JWT token has expired |
| `INSUFFICIENT_PERMISSIONS` | User lacks required role |
| `TENANT_MISMATCH` | Resource does not belong to user's tenant |
| `NOT_FOUND` | Requested resource does not exist |
| `CONFLICT` | Resource state conflict |
| `RATE_LIMIT_EXCEEDED` | Too many requests |
| `PAYLOAD_TOO_LARGE` | File exceeds size limit |
| `UNSUPPORTED_FILE_TYPE` | Document format not supported |

## API Endpoints

### Auth

```
POST   /api/v1/auth/login                  Login with email/password
POST   /api/v1/auth/refresh                Refresh JWT token
POST   /api/v1/auth/logout                 Invalidate current session
GET    /api/v1/auth/me                     Get current user profile
POST   /api/v1/auth/entra-id              Authenticate via Azure Entra ID
```

### Chat

```
POST   /api/v1/chat/conversations               Create conversation
GET    /api/v1/chat/conversations                List conversations (paginated)
GET    /api/v1/chat/conversations/{id}           Get conversation details
PUT    /api/v1/chat/conversations/{id}           Update conversation (title, archive)
DELETE /api/v1/chat/conversations/{id}           Delete conversation
POST   /api/v1/chat/conversations/{id}/messages  Send message (returns AI response)
GET    /api/v1/chat/conversations/{id}/messages  Get messages (paginated)
POST   /api/v1/chat/stream                       Stream AI response via SSE
POST   /api/v1/chat/suggested-actions            Get suggested follow-up actions
```

### Documents

```
POST   /api/v1/documents/upload                 Upload document (multipart)
GET    /api/v1/documents                        List documents (paginated, filterable)
GET    /api/v1/documents/{id}                   Get document metadata
DELETE /api/v1/documents/{id}                   Soft-delete document
POST   /api/v1/documents/{id}/process           Trigger AI processing (OCR, chunking)
GET    /api/v1/documents/{id}/download          Download original file
POST   /api/v1/documents/{id}/summarize         Generate AI summary
POST   /api/v1/documents/{id}/extract           Extract structured data
POST   /api/v1/documents/compare                Compare two documents
POST   /api/v1/documents/{id}/ask               Q&A against document content
```

### Knowledge Bases

```
POST   /api/v1/knowledge-bases                  Create knowledge base
GET    /api/v1/knowledge-bases                  List knowledge bases
GET    /api/v1/knowledge-bases/{id}             Get knowledge base details
PUT    /api/v1/knowledge-bases/{id}             Update knowledge base
DELETE /api/v1/knowledge-bases/{id}             Delete knowledge base
POST   /api/v1/knowledge-bases/{id}/sources     Add source (document, website, text)
DELETE /api/v1/knowledge-bases/{id}/sources/{s} Remove source
POST   /api/v1/knowledge-bases/{id}/train       Generate embeddings
POST   /api/v1/knowledge-bases/{id}/search      Semantic search against KB
```

### Chatbots

```
POST   /api/v1/chatbots                         Create chatbot
GET    /api/v1/chatbots                         List chatbots
GET    /api/v1/chatbots/{id}                    Get chatbot configuration
PUT    /api/v1/chatbots/{id}                    Update chatbot settings
DELETE /api/v1/chatbots/{id}                    Delete chatbot
POST   /api/v1/chatbots/{id}/train              Train on selected sources
POST   /api/v1/chatbots/{id}/publish            Publish chatbot widget
GET    /api/v1/chatbots/{id}/script             Get embeddable widget script
POST   /api/v1/chatbots/widget/message          Public widget chat endpoint (no auth)
```

### Proposals

```
POST   /api/v1/proposals                        Create proposal from RFP
GET    /api/v1/proposals                        List proposals
GET    /api/v1/proposals/{id}                   Get full proposal content
DELETE /api/v1/proposals/{id}                   Delete proposal
POST   /api/v1/proposals/{id}/generate          Generate proposal via AI
GET    /api/v1/proposals/{id}/export/{format}   Export as PDF/DOCX
```

### Workflows

```
POST   /api/v1/workflows                        Create workflow definition
GET    /api/v1/workflows                        List workflow definitions
GET    /api/v1/workflows/{id}                   Get workflow with steps
PUT    /api/v1/workflows/{id}                   Update workflow definition
DELETE /api/v1/workflows/{id}                   Delete workflow
POST   /api/v1/workflows/{id}/execute           Execute workflow instance
GET    /api/v1/workflows/instances              List workflow instances
GET    /api/v1/workflows/instances/{id}         Get instance status & data
POST   /api/v1/workflows/instances/{id}/approve Approve current step
POST   /api/v1/workflows/instances/{id}/reject  Reject current step
```

### Assistants

```
POST   /api/v1/assistants                       Create enterprise assistant
GET    /api/v1/assistants                       List assistants (filter by type)
GET    /api/v1/assistants/{id}                  Get assistant config
PUT    /api/v1/assistants/{id}                  Update assistant
DELETE /api/v1/assistants/{id}                  Delete assistant
POST   /api/v1/assistants/{id}/chat             Chat with enterprise assistant
```

### Admin

```
GET    /api/v1/admin/users                      List users (paginated, filterable)
POST   /api/v1/admin/users                      Create user
PUT    /api/v1/admin/users/{id}                 Update user
DELETE /api/v1/admin/users/{id}                 Delete user
GET    /api/v1/admin/roles                      List roles
POST   /api/v1/admin/roles                      Create role
PUT    /api/v1/admin/roles/{id}                 Update role permissions
DELETE /api/v1/admin/roles/{id}                 Delete role
GET    /api/v1/admin/audit-logs                 Query audit log (paginated)
GET    /api/v1/admin/settings                   Get tenant settings
PUT    /api/v1/admin/settings                   Update tenant settings
GET    /api/v1/admin/models                     List AI model configurations
POST   /api/v1/admin/models                     Add AI model configuration
PUT    /api/v1/admin/models/{id}                Update model config
DELETE /api/v1/admin/models/{id}                Remove model config
GET    /api/v1/admin/analytics                  Usage analytics & metrics
```

## WebSocket / SignalR

### Connection

```javascript
// Browser
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/chat", {
    accessTokenFactory: () => getAuthToken()
  })
  .withAutomaticReconnect()
  .build();
```

### Hubs

| Hub | Path | Events |
|---|---|---|
| ChatHub | `/hubs/chat` | AI streaming, typing indicators |
| StatusHub | `/hubs/status` | Document processing progress, task status |
| NotificationHub | `/hubs/notification` | Workflow actions, system alerts |

### ChatHub Events

**Client → Server:**

| Event | Payload | Description |
|---|---|---|
| `SendMessage` | `{ conversationId, content, attachments }` | Send chat message |
| `StreamMessage` | `{ conversationId, content }` | Request streaming AI response |
| `Typing` | `{ conversationId }` | Indicate user is typing |
| `CancelStream` | `{ conversationId }` | Cancel ongoing AI stream |
| `DeleteMessage` | `{ messageId }` | Delete a message |

**Server → Client:**

| Event | Payload | Description |
|---|---|---|
| `ReceiveMessage` | `{ id, conversationId, role, content, timestamp }` | Complete message received |
| `StreamToken` | `{ conversationId, token }` | Streaming token chunk |
| `StreamEnd` | `{ conversationId, messageId }` | Stream completed |
| `StreamError` | `{ conversationId, error }` | Stream encountered an error |
| `MessageStatus` | `{ conversationId, messageId, status }` | Message status update |
| `TypingIndicator` | `{ conversationId, userId, isTyping }` | Other user's typing state |

### StatusHub Events

**Client → Server:**

| Event | Payload | Description |
|---|---|---|
| `SubscribeTask` | `{ taskId }` | Subscribe to task progress updates |

**Server → Client:**

| Event | Payload | Description |
|---|---|---|
| `ProcessingUpdate` | `{ taskId, progress, status, message }` | Task progress (0-100%) |
| `TaskCompleted` | `{ taskId, result }` | Task completed successfully |
| `TaskFailed` | `{ taskId, error }` | Task failed |

### NotificationHub Events

**Server → Client:**

| Event | Payload | Description |
|---|---|---|
| `NotificationReceived` | `{ id, type, title, body, data }` | New notification |
| `WorkflowActionRequired` | `{ workflowId, instanceId, stepName }` | Approval needed |
| `DocumentProcessed` | `{ documentId, status }` | Document processing done |

## Pagination

List endpoints support cursor-based pagination:

```http
GET /api/v1/chat/conversations?limit=20&cursor=eyJpZCI6InV1aWQifQ==
```

Response:

```json
{
  "data": [...],
  "pagination": {
    "nextCursor": "eyJpZCI6Im5leHQtdXVpZCJ9",
    "hasMore": true
  }
}
```

## Versioning

The API is versioned via URL prefix (`/api/v1/`). Breaking changes will increment the version number. Deprecated versions will be supported for at least 90 days.

## CORS

In production, CORS is restricted to `https://app.r2wai.com`. For local development, `https://localhost:3000` and `http://localhost:3001` are allowed.

## SDK / Client Libraries

- **TypeScript**: `@r2wai/api-client` (planned)
- **Python**: `r2wai-client` (planned)
- **OpenAPI Spec**: Available via `/swagger/v1/swagger.json`
