#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

project_version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)"
server_version_values="$(grep -o '"version": "[^"]*"' src/Beesum.Mcp/.mcp/server.json | sed 's/.*"\([^"]*\)"$/\1/')"

if [ -z "$server_version_values" ]; then
  echo "server.json has no version fields" >&2
  exit 1
fi

for server_version in $server_version_values; do
  if [ "$server_version" != "$project_version" ]; then
    echo "server.json version $server_version does not match project version $project_version" >&2
    exit 1
  fi
done

rm -rf artifacts/packages
mkdir -p artifacts/packages

dotnet pack src/Beesum.Core/Beesum.Core.csproj -c Release -o artifacts/packages
dotnet pack src/Beesum.Testing/Beesum.Testing.csproj -c Release -o artifacts/packages
dotnet pack src/Beesum.Min.Cli/Beesum.Min.Cli.csproj -c Release -o artifacts/packages
dotnet pack src/Beesum.Mcp/Beesum.Mcp.csproj -c Release -o artifacts/packages

cd artifacts/packages
sha256sum ./*.nupkg ./*.snupkg > SHA256SUMS
ls -lh
