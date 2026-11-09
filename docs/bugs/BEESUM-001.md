# BEESUM-001 — crash when `%TEMP%` is not writable

**Status:** fixed in v0.2.0
**Severity:** high (blocked the main GUI scenario)
**Component:** `Beesum.Core.Pipeline.SummarizerPipeline`

## Symptom

Summarizing a URL failed with `UnauthorizedAccessException` in
`Directory.CreateDirectory` for `%TEMP%\beesum-<guid>`. The GUI showed
`Unexpected error.`; the CLI exited with `Fatal: ...`. The pipeline aborted before
downloading anything.

## Root cause

The pipeline created its working directory directly under `%TEMP%` and did not
handle failures. `%TEMP%` can be unwritable even for a normal user: antivirus/EDR
filtering, group policy, a custom `TEMP` variable, a locked-down profile, or a
low-integrity process token. The exception escaped to the host and was reported as
an unexpected error.

The report's claim that `--local-vtt` also failed does not match the code: the
local-VTT branch never creates a working directory.

## Fix

`SummarizerPipeline.CreateWorkDirectory()` now uses a fallback chain:

1. the configured `subtitlesDirectory` (probed for writability with a temporary
   file);
2. `Directory.CreateTempSubdirectory("beesum-")`;
3. `%APPDATA%\Beesum\temp\<guid>`;
4. `<app>\temp\<guid>` next to the executable;
5. otherwise a clear `SummaryResult` error instead of an exception.

Fallback directories are deleted after the run; a configured directory is kept.

## Tests

- `RunAsync_ConfiguredSubtitlesDirectoryNotWritable_FallsBackToWritableDirectory`
- `RunAsync_ConfiguredSubtitlesDirectoryWritable_IsUsedAndKept`

Manual check: run the CLI with `TEMP`/`TMP` pointing at a regular file; the
pipeline falls back to `%APPDATA%\Beesum\temp` and reaches the download stage.

## Notes

- Not an administrator-rights issue.
- Choosing a shared persistent `subtitlesDirectory` now would be unsafe:
  `DownloadSubtitlesAsync` picks the first `*.vtt` in the directory, so a shared
  cache could return a subtitle file from another video. Persistent caching is
  planned for v0.1.1 together with `ITranscriptCache`.
