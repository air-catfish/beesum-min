# Good first issues

Scoped starter tasks. Copy one into a GitHub issue (or pick it up directly), keep the change buildable with tests, and open an issue first for anything large.

Each item lists the goal, the files that are usually involved, and a definition of
done. Sizes are rough: S is an afternoon, M is a day or two.

---

## 1. Wire `ITranscriptCache` into the pipeline (M)

**Goal.** Avoid re-downloading subtitles for the same video.

**Where.** `src/Beesum.Core/Pipeline/SummarizerPipeline.cs`,
`src/Beesum.Core/Storage/FileTranscriptCache.cs`,
`src/Beesum.Core/DependencyInjection/ServiceCollectionExtensions.cs`.

**Done when.** A cached transcript is used when present and fresh, a cache hit is
observable in the result/progress, cache writes are atomic, and tests cover hit,
miss, stale entry, and corruption.

---

## 2. Persist summary history (M)

**Goal.** Make `ISummaryHistory` real instead of an unused contract.

**Where.** `src/Beesum.Core/Pipeline/SummarizerPipeline.cs`,
`src/Beesum.Core/Storage/JsonSummaryHistory.cs`.

**Done when.** Every successful run appends an entry (id, source id, title,
provider, style, timestamp), the JSONL format has a documented schema, and tests
cover append, read, remove, and corrupt lines.

---

## 3. First-run yt-dlp setup (M)

**Goal.** Remove the "install yt-dlp yourself" step for CLI users.

**Where.** `src/Beesum.Core/YtDlp/YtDlpPathResolver.cs`,
`src/Beesum.Core/YtDlp/YtDlpService.cs`, CLI flags.

**Done when.** A new `beesum-min --install-yt-dlp` command downloads the binary
into `tools/` with progress and checksum/size sanity checks, never runs without
the explicit flag, and has tests for the happy path and HTTP failure.

---

## 4. Actionable yt-dlp error messages (S)

**Goal.** Turn raw yt-dlp stderr into guidance.

**Where.** `src/Beesum.Core/YtDlp/YtDlpService.cs`,
`src/Beesum.Core/Models/SubtitleDownloadResult.cs`.

**Done when.** 429, 403, "Sign in to confirm you're not a bot", DNS failure, and
timeouts map to short messages with the suggested next step (sleep, cookies,
proxy, JS runtime), covered by tests with representative stderr samples.

---

## 5. Golden-file tests for real-world WebVTT variants (S)

**Goal.** Harden the parser against formats not covered by hand-written fixtures.

**Where.** `tests/Beesum.Core.Tests/Parsing/`, new `fixtures/*.vtt`.

**Done when.** At least five sanitized real-world files (YouTube auto-captions,
manual captions, styled cues, multi-line cues, non-English) are parsed and
compared against reviewed expected text; the fixtures contain no personal data.

---

## 6. Make the CLI trim- and AOT-friendly (M)

**Goal.** `-p:PublishTrimmed=true` currently breaks the LLM providers because
`PostAsJsonAsync` uses reflection-based `System.Text.Json`.

**Where.** `src/Beesum.Core/Llm/OllamaLlmProvider.cs`,
`OpenAiCompatibleLlmProvider.cs`, `src/Beesum.Min.Cli/Beesum.Min.Cli.csproj`.

**Done when.** Request/response payloads use a source-generated
`JsonSerializerContext`, a trimmed publish runs the local-vtt and Ollama smoke
tests, and the measured size is documented.

---

## 7. `--yt-dlp-args-file` and escape support (S)

**Goal.** Make complex yt-dlp invocations maintainable.

**Where.** `src/Beesum.Min.Cli/CliOptionsParser.cs`, `HelpText.cs`,
`tests/Beesum.Min.Cli.Tests/`.

**Done when.** `--yt-dlp-args-file <path>` reads arguments from a file (one
argument per line, `#` comments), backslash escapes such as `\"` work in
`--yt-dlp-args`, and tests cover quoting, escapes, comments, and invalid files.

---

## 8. Batch mode (M)

**Goal.** Summarize a list of URLs in one invocation.

**Where.** `src/Beesum.Min.Cli/`, `src/Beesum.Core/Pipeline/`.

**Done when.** `--batch <file>` accepts one URL per line, reports per-item success
and failure, supports `--json` as JSON Lines or an array, keeps a single provider
instance, and is covered by tests.

---

## 9. Docker image smoke test in CI (S)

**Goal.** Turn the local `Dockerfile` into a tested artifact.

**Where.** `.github/workflows/ci.yml`, `Dockerfile`.

**Done when.** CI builds the Linux image, runs `docker run --rm beesum-min --help`
and a local `.vtt` smoke test, and the README shows the real image size.

---

## 10. JSON contract test (S)

**Goal.** Lock the `--json` shape so scripts do not break silently.

**Where.** `tests/Beesum.Min.Cli.Tests/JsonOutputTests.cs`, new
`fixtures/json/*.json`.

**Done when.** Serialized output is compared against reviewed golden files for
success, failure, and metadata-present cases, and the expected field set is
asserted explicitly.
