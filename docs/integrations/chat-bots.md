# Chat bots and web clients (future)

The local CLI stays local. Bots and web clients should talk to a server API, not
run `yt-dlp` on the bot host.

## Flow

```text
User sends URL
      │
      ▼
Connector verifies webhook / auth
      │
      ▼
POST /v1/jobs  ──► 202 jobId
      │
      ▼
Connector sends "processing"
      │
      ▼
Poll GET /v1/jobs/{id} or receive webhook
      │
      ▼
Send summary + transcript links
```

## Connectors

- Telegram: verify the secret token, map chat/user to tenant.
- Discord: verify interaction signature, defer the response.
- Slack: verify signing secret, use `response_url`.
- Web: normal session/OIDC auth.

Connectors are adapters. They must not own authz, quotas, sandboxing, or storage
policy.

## Formatting

Chat platforms have message limits. Send a short summary plus a link to the full
Markdown artifact; do not paste a 10k-character transcript into a message.

## Failure modes

- `yt-dlp` failure: show a retryable error, not a stack trace.
- LLM failure: keep the transcript artifact and let the user retry the summary.
- Timeout: keep the job alive in the queue; the connector should not block.
