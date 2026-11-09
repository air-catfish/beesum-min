# Security

## Model

`beesum-min` is a local command-line tool. `beesum-mcp` is a local stdio MCP
server: one user, one process, no network listener, no authentication. Do not
expose either to an untrusted network.

## Secrets

API keys come from environment variables or the platform secret store. Windows
uses DPAPI. Unix uses a user-only `0o600` file; it is **not encrypted yet**, so
the OS account is the boundary. Do not run it under a shared account, and do not
put keys in command-line arguments or committed files.

## Data

With `transcript-only` (default) no transcript leaves the machine. With
`ollama`, it goes only to the configured local endpoint. Cloud providers receive
the transcript and prompt by design. Transcripts and history are written with
`0o600` files and `0o700` directories on Unix.

`yt-dlp` runs as a child process with an argument list, never through a shell.
Cookies, browser profiles, and proxies are opt-in.

## MCP

`summarize_url` accepts only YouTube hosts; `file://`, arbitrary hosts, and local
paths are rejected. MCP tools do not write files. The agent still controls the
prompt, so treat model output as untrusted text: prompt injection in a transcript
can change what the summary says, but cannot execute commands through beesum-min.

## Hashes and signatures

Git commit hashes are integrity checks, not proof of authorship. Release assets
carry `SHA256SUMS`; Docker pins `yt-dlp` and `deno` and verifies their published
hashes. NuGet packages are not code-signed yet. API keys are not hashed: the
client must send the original key, so hashing at rest would make it unusable;
they are encrypted (Windows) or permission-protected (Unix) instead.

## Container

The image runs as UID/GID `10001`, not root. `yt-dlp` and `deno` are pinned and
verified with SHA-256 checksums; `.nupkg` files get a `SHA256SUMS` file. Base
images are pinned by digest, and GitHub Actions are pinned to commit SHAs.

## Known limitations

- Unix secrets are permission-protected, not encrypted.
- No package signing or SBOM yet; checksums only. NuGet packages are not signed.
- No rate limiting; a local caller can submit a very large transcript.
- Prompt-injected summaries can be misleading; verify important claims.

## Reporting

Use **GitHub → Security → Advisories**. Include the version, a minimal
reproduction, and the impact. Do not open a public issue. Only the latest `0.x`
version is supported.
