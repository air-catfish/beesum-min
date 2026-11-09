# Server security model

For a multi-tenant deployment, every item below is mandatory.

## Identity and tenancy

- OIDC for people, scoped API keys for bots.
- `tenant_id` on every row, cache key, object path, and queue message.
- RBAC checks on every job/artifact read.
- Row-level security or a single tenant-aware repository layer.

## Worker isolation

- Separate worker service; no public ingress.
- Non-root container, read-only rootfs, seccomp, no host mounts.
- Egress allowlist; reject `file://`, localhost, link-local and private IPs.
- Per-job temp directory, cleaned up in `finally`.
- CPU/RAM/time limits; kill the whole process tree.
- No user-provided `yt-dlp` arguments, cookies, or paths.

## Queues and quotas

- Idempotency key per request.
- Per-tenant concurrency and daily quotas.
- Retry with backoff, dead-letter queue, cancellation.
- Input and token limits before calling an LLM.

## Secrets and storage

- KMS/envelope encryption for per-tenant keys and cookies.
- Tenant-prefixed object storage; signed, short-lived download URLs.
- No transcripts or keys in logs.
- Retention, deletion, and export policies.

## API and output

- Request validation, payload limits, CORS/CSRF, security headers.
- Markdown/HTML sanitization and CSP.
- Model output is untrusted; do not execute instructions found in a transcript.

## Operations

- Audit log, metrics without transcript content, traces, alerts.
- Backups and tested restore.
- Signed images/SBOM, dependency scanning, isolated network.
- Authz, SSRF, path traversal, resource-exhaustion, and prompt-injection tests.
