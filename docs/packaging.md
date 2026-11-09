# Packages

Four packages are produced by `scripts/pack.sh`:

| Package | Kind | Contents |
| --- | --- | --- |
| `Beesum.Core` | library | parser, pipeline, providers, storage |
| `Beesum.Testing` | library | in-memory test doubles |
| `beesum-min` | .NET tool | the CLI |
| `beesum-mcp` | .NET tool | the MCP stdio server |

`SHA256SUMS` is written next to them. Symbol packages (`.snupkg`) are produced too.

## Build packages locally

```bash
bash scripts/pack.sh
```

## Install

Once a release is published:

```powershell
dotnet tool install -g beesum-min
dotnet tool install -g beesum-mcp
dotnet add package Beesum.Core
```

Until then, build from source (see the README).

## GitHub Packages

Run **Actions → Packages → Run workflow** to publish to GitHub Packages without a
tag or a NuGet key. The release workflow publishes the same packages on a tag.
That source requires authentication, even for public packages:

```powershell
dotnet nuget add source "https://nuget.pkg.github.com/air-catfish/index.json" `
  --name github --username air-catfish --password "<PAT with read:packages>"
```

Then install with `--add-source github`, or put it in `nuget.config`.

## Release checklist

1. Set the version in `Directory.Build.props` (currently `0.1.0`).
2. Add the `NUGET_API_KEY` repository secret (nuget.org key with “Push new
   packages” scope) if publishing to nuget.org.
3. Create a signed tag and push it:

   ```bash
   git tag -s v0.1.0 -m "beesum-min 0.1.0"
   git verify-tag v0.1.0
   git push origin v0.1.0
   ```

   If you do not have a signing key, create an unsigned tag and enable immutable
   releases in the repository settings; it is weaker.

4. The workflow runs tests, packs, publishes to GitHub Packages, optionally pushes
   to nuget.org with symbols, builds the Docker image, and attaches the packages
   and `SHA256SUMS` to the GitHub release.

`NUGET_API_KEY` is optional: without it the release still publishes to GitHub
Packages and finishes. With it, `dotnet tool install` works from nuget.org.
