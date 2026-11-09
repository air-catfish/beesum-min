# Install

## .NET tool

Requires the .NET 10 runtime.

```powershell
dotnet tool install -g beesum-min
dotnet tool install -g beesum-mcp
```

## Library

```powershell
dotnet add package Beesum.Core
dotnet add package Beesum.Testing   # test projects
```

## Docker

```bash
docker run --rm -v "$PWD:/out" ghcr.io/air-catfish/beesum-min:latest \
  "https://www.youtube.com/watch?v=..." -o /out/summary.md
```

The image includes `yt-dlp` and `deno`.

## From source

```powershell
dotnet build beesum-min.slnx -c Release
dotnet run --project src\Beesum.Min.Cli -- --local-vtt samples\demo\talk.vtt
```

`yt-dlp` must be in `PATH`, in `tools/` next to the executable, or passed with
`--yt-dlp`.

## Check the environment

```powershell
beesum-min doctor
beesum-min doctor --check-updates
```
