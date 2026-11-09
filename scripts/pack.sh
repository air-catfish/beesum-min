#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

rm -rf artifacts/packages
mkdir -p artifacts/packages

dotnet pack src/Beesum.Core/Beesum.Core.csproj -c Release -o artifacts/packages
dotnet pack src/Beesum.Testing/Beesum.Testing.csproj -c Release -o artifacts/packages
dotnet pack src/Beesum.Min.Cli/Beesum.Min.Cli.csproj -c Release -o artifacts/packages
dotnet pack src/Beesum.Mcp/Beesum.Mcp.csproj -c Release -o artifacts/packages

cd artifacts/packages
sha256sum ./*.nupkg ./*.snupkg > SHA256SUMS
ls -lh