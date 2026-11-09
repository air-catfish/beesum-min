#!/usr/bin/env bash
# Manual smoke test for a Linux/macOS checkout. Needs .NET 10 SDK.
# URL downloads are not tested here; they need yt-dlp and the network.

set -euo pipefail
cd "$(dirname "$0")/.."

echo "== build =="
dotnet build beesum-min.slnx -c Release -p:NuGetAudit=false

echo "== tests =="
dotnet run --project tests/Beesum.Core.Tests -c Release --no-build
dotnet run --project tests/Beesum.Min.Cli.Tests -c Release --no-build
dotnet run --project tests/Beesum.Testing.Tests -c Release --no-build
dotnet run --project tests/Beesum.Mcp.Tests -c Release --no-build

echo "== mcp version =="
dotnet run --project src/Beesum.Mcp -c Release --no-build -- --version

echo "== sample =="
dotnet run --project samples/01-quick-start -c Release --no-build -- samples/demo/smoke.vtt

echo "== cli: local vtt =="
dotnet run --project src/Beesum.Min.Cli -c Release --no-build -- --local-vtt samples/demo/smoke.vtt

echo "== cli: json =="
json="$(dotnet run --project src/Beesum.Min.Cli -c Release --no-build -- --local-vtt samples/demo/smoke.vtt --json)"
printf '%s\n' "$json"
grep -q '"isSuccess": true' <<<"$json"
grep -q '"providerId": "transcript-only"' <<<"$json"

echo "== cli: stdin vtt =="
json="$(cat samples/demo/smoke.vtt | dotnet run --project src/Beesum.Min.Cli -c Release --no-build -- --stdin --input vtt --json)"
printf '%s\n' "$json"
grep -q '"isSuccess": true' <<<"$json"

echo "== cli: missing API key exits 2 =="
set +e
dotnet run --project src/Beesum.Min.Cli -c Release --no-build -- "https://example.invalid" --provider anthropic >/tmp/beesum-smoke-out 2>/tmp/beesum-smoke-err
status=$?
set -e
if [[ $status -ne 2 ]]; then
  echo "expected exit 2, got $status"
  cat /tmp/beesum-smoke-err
  exit 1
fi

echo "smoke: ok"