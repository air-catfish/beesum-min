# syntax=docker/dockerfile:1

# Build: self-contained Linux CLI.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props beesum-min.slnx ./
COPY src/Beesum.Core/Beesum.Core.csproj src/Beesum.Core/
COPY src/Beesum.Min.Cli/Beesum.Min.Cli.csproj src/Beesum.Min.Cli/
RUN dotnet restore src/Beesum.Min.Cli/Beesum.Min.Cli.csproj -p:NuGetAudit=false
COPY src/ src/
RUN dotnet publish src/Beesum.Min.Cli/Beesum.Min.Cli.csproj \
        -c Release -r linux-x64 --self-contained true \
        -p:NuGetAudit=false -o /app

# Runtime: no .NET runtime needed, the app is self-contained.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates unzip \
    && rm -rf /var/lib/apt/lists/*

# yt-dlp_linux is a self-contained binary (no Python needed). It lives in PATH,
# which YtDlpPathResolver checks as the third fallback.
ADD https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux /usr/local/bin/yt-dlp
# YouTube requires a JavaScript runtime for its challenges; yt-dlp only
# auto-detects deno. Without it downloads fail with "No supported JavaScript
# runtime could be found".
ADD https://github.com/denoland/deno/releases/latest/download/deno-x86_64-unknown-linux-gnu.zip /tmp/deno.zip
RUN chmod 0755 /usr/local/bin/yt-dlp \
    && unzip /tmp/deno.zip -d /usr/local/bin \
    && chmod 0755 /usr/local/bin/deno \
    && rm /tmp/deno.zip

WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["/app/beesum-min"]
