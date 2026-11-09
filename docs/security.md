# Security

The canonical policy is [SECURITY.md](https://codeberg.org/air-catfish/beesum-min/src/branch/main/SECURITY.md)
in the repository.

## Short version

- Local CLI; MCP is local stdio. Do not expose either to an untrusted network.
- `transcript-only` never sends the transcript anywhere. Ollama stays local.
- API keys come from env variables or the local secret store.
- Windows uses DPAPI; Unix uses a `0600` file, not encryption yet.
- `yt-dlp` runs as a child process with an argument list, not through a shell.
- MCP `summarize_url` accepts YouTube hosts only.
- Docker runs as UID/GID `10001`; `yt-dlp` and `deno` are pinned and checksummed.
- No telemetry, no background network activity.

## Known limitations

- Unix secrets are permission-protected, not encrypted.
- NuGet packages are not signed; there is no SBOM yet.
- No rate limit for a local caller.
- Prompt-injected transcripts can make a summary misleading; no code execution.
- The git tag is not signed.

## Report

Use GitHub Security Advisories, or the Codeberg issue tracker if GitHub is not
available. Do not open a public issue for an unfixed vulnerability.
