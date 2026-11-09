# syntax=docker/dockerfile:1

# Build: self-contained Linux CLI.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
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
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.4@sha256:cd7a01db7af22938a7e5cd938aa0e1a4d0edfb96b7134476c31c954197b5461e AS runtime
ARG YTDLP_VERSION=2026.10.26
ARG YTDLP_SHA256=767ba9dce7052aa196f8b12652f433d76de128ba168966fa84b6394d5e6c6795
ARG DENO_VERSION=2.9.8
ARG DENO_SHA256=e890975bc2549807bbcc0b584ffafda72cc7c93349dd99c80483dd784f6c4609
ARG DENO_MIRROR_SHA256=a39762429792913fe762ef0b59c9cba41dbd38195984da99221a193b13d97b40

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl unzip \
    && rm -rf /var/lib/apt/lists/*

# Pin both downloads and verify the published SHA-256 checksums.
RUN set -eux; \
    curl -fsSL --retry 5 --retry-all-errors --retry-delay 2 -o /usr/local/bin/yt-dlp \
      "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_linux"; \
    echo "${YTDLP_SHA256}  /usr/local/bin/yt-dlp" | sha256sum -c -; \
    chmod 0755 /usr/local/bin/yt-dlp; \
    curl -fsSL --retry 5 --retry-all-errors --retry-delay 2 -o /tmp/deno.zip \
      "https://github.com/denoland/deno/releases/download/v${DENO_VERSION}/deno-x86_64-unknown-linux-gnu.zip" \
      || curl -fsSL --retry 5 --retry-all-errors --retry-delay 2 -o /tmp/deno.zip \
        "https://dl.deno.land/release/v${DENO_VERSION}/deno-x86_64-unknown-linux-gnu.zip"; \
    if ! echo "${DENO_SHA256}  /tmp/deno.zip" | sha256sum -c - 2>/dev/null; then \
      echo "${DENO_MIRROR_SHA256}  /tmp/deno.zip" | sha256sum -c -; \
    fi; \
    unzip /tmp/deno.zip -d /usr/local/bin; \
    chmod 0755 /usr/local/bin/deno; \
    rm /tmp/deno.zip

WORKDIR /app
COPY --from=build /app ./
RUN chown -R 10001:10001 /app
USER 10001:10001
ENV HOME=/tmp
ENTRYPOINT ["/app/beesum-min"]
