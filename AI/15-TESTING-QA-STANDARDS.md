# Testing & QA Standards

## Testing layers

```text
Unit
Integration
End-to-End
AI Evaluation
Security
Performance
```

## Unit tests

Prioritize:
- parameter resolution
- parameter validation
- policy evaluation
- authorization
- configuration validation
- domain rules

## Integration tests

Cover:
- OpenAPI discovery
- provider connectivity
- tool execution
- database access
- knowledge ingestion
- retrieval
- audit
- approval
- publishing

## End-to-end

Minimum critical path:

```text
Create workspace
→ Create agent
→ Connect API
→ Discover
→ Generate tool
→ Test
→ Publish
→ Execute
→ Audit
```

## AI evaluation

Create repeatable datasets for:
- RAG questions
- tool selection
- parameter resolution
- citations
- invalid requests
- denied permissions
- approval-required actions
- API failures

Measure:
- correctness
- groundedness
- tool selection accuracy
- parameter accuracy
- citation accuracy
- execution success
- latency
- failure rate

## Security tests

Test:
- tenant isolation
- workspace isolation
- RBAC
- unauthorized tool calls
- SSRF
- secret exposure
- injection
- rate limits
- audit

## Regression

Any change to:
- agent runtime
- tool gateway
- authorization
- connection providers
- knowledge retrieval
- publishing

requires regression testing of the flagship flows.

## Release gate

Do not declare production-ready when critical tests are failing.
