# Observability & Performance Standards

## Execution identifiers

Where applicable, track:
- correlation ID
- execution ID
- tenant/workspace
- agent
- tool
- provider

## Useful operational signals

- API latency
- model latency
- tool latency
- RAG latency
- queue depth
- execution duration
- failures
- retries
- database latency
- connection pool pressure
- token/usage metrics where available

## Logging

Logs should be:
- structured
- searchable
- correlated
- useful for diagnosis

Never log secrets.

Minimize sensitive data.

## Performance

Prefer:
- async I/O
- streaming
- bounded retries
- timeouts
- cancellation
- caching where safe
- parallel independent retrieval
- queue-based long-running execution

## Scalability direction

```text
CDN / WAF / Gateway
→ Web / Widget
→ API cluster
→ Execution queue
→ Workers
→ Model gateway
→ Providers
```

Data may use:
- PostgreSQL
- Redis
- object storage

Verify actual repository usage before making changes.

## Large tables

Execution, activity, audit and usage data can grow rapidly.

Consider:
- retention
- archival
- indexing
- partitioning when justified

Do not prematurely partition everything.

## Performance investigation

Use:

```text
Measure
→ Identify bottleneck
→ Reproduce
→ Fix
→ Benchmark
→ Regression test
```

Never optimize based only on assumption.
