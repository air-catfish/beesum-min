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

From nuget.org:

```powershell
dotnet tool install -g beesum-min
dotnet tool install -g beesum-mcp
dotnet add package Beesum.Core
dotnet add package Beesum.Testing   # in test projects
```

## nuget.org: Trusted Publishing

No long-lived `NUGET_API_KEY` is stored. The `NuGet` workflow uses GitHub OIDC via
`NuGet/login@v1`; the token lives for one job.

One-time setup:

1. **nuget.org → Account → Trusted Publishing → Add policy**
   - Repository owner: `air-catfish`
   - Repository: `beesum-min`
   - Workflow file: `nuget.yml` (the filename, not the workflow display name)
   - Environment: `release`
2. **GitHub → Settings → Environments → New environment → `release`**
   - Add an **environment secret**: `NUGET_USER` = your nuget.org profile name,
     not your email.
   - Optional: required reviewers as a publish gate.
3. Push a `v*` tag, or run **Actions → NuGet → Run workflow** with an existing tag
   (for example `v0.2.1`).

The policy is permanent for a public repository after the first successful
publish; private repositories have a seven-day temporary window.

## GitHub Packages

The release workflow publishes the same packages to GitHub Packages with
`GITHUB_TOKEN`. That source requires authentication for consumers, even when the
packages are public:

```powershell
dotnet nuget add source "https://nuget.pkg.github.com/air-catfish/index.json" `
  --name github --username air-catfish --password "<PAT with read:packages>"
```

Then install with `--add-source github`, or put it in `nuget.config`.

## Release checklist

1. Set the version in `Directory.Build.props` (currently `0.2.1`).
2. Create the trusted publishing policy and the `release` environment secret
   `NUGET_USER` (once).
3. Create a signed tag and push it:

   ```bash
   git tag -s v0.2.1 -m "beesum-min 0.2.1"
   git verify-tag v0.2.1
   git push origin v0.2.1
   ```

   If you do not have a signing key, create an unsigned tag and enable immutable
   releases in the repository settings; it is weaker.

4. The tag runs two workflows: `Release` (GitHub Packages, Docker image, GitHub
   release with packages and `SHA256SUMS`) and `NuGet` (nuget.org with symbols).
   For an already existing tag, dispatch `NuGet` manually.
