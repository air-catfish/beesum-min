# Tenancy

The current local storage (`FileTranscriptCache`, `JsonSummaryHistory`,
`FileSettingsStore`) is designed for one user. A server needs tenant-aware
replacements.

## Minimum model

```text
Tenant 1─* User
Tenant 1─* Job 1─* Artifact
Tenant 1─* ApiKey
Tenant 1─* AuditEvent
```

## Rules

- Every query includes `tenant_id`; no unscoped `Find(id)`.
- Object keys: `tenants/{tenantId}/jobs/{jobId}/{kind}`.
- Cache keys include tenant and source; a global transcript cache is a leak.
- Quotas are per tenant, not per process.
- Deletion removes database rows, objects, caches, and queue messages.
- An admin UI must not bypass tenant filters by accident.

## Testing

Write an integration test where tenant A requests tenant B's job/artifact and
expects 404 (not 403, to avoid existence leaks). Run it for every endpoint.
