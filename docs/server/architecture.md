# Server architecture (future)

This page describes the planned service layer. It is **not** implemented in the
`beesum-min` repository.

```text
CLI / MCP / Telegram / Discord / Web
                │
          HTTPS + webhooks
                ▼
        Beesum.Server (ASP.NET)
  auth, tenants, quotas, validation,
  jobs, artifacts, audit
                │
                ▼
        Beesum.Worker (container)
  yt-dlp sandbox + Beesum.Core + LLM
                │
                ▼
        Postgres / Redis / S3
```

## Repository split

- `beesum-min` — local CLI, Core package, MCP; no server dependencies.
- `beesum-server` — ASP.NET API, worker, connectors; references `Beesum.Core`
  as a NuGet package.
- `Beesum.Connectors.*` — Telegram/Discord/Slack adapters; call the HTTP API,
  never `yt-dlp` directly.

## API shape

```text
POST /v1/jobs          -> 202 { jobId }
GET  /v1/jobs/{id}     -> status, result, usage
GET  /v1/jobs/{id}/artifacts/{kind}
```

Long work is queued; the API process never downloads subtitles itself.

## Why not reuse the CLI process

Running a CLI process per request is possible for a prototype, but it lacks
tenant isolation, quotas, retries, and structured cancellation. The worker should
call `Beesum.Core` directly.
