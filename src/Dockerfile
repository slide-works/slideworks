# Build context is src/: docker build -t slide-works src

# Stage 1: build the SPA straight into the API project's wwwroot.
FROM node:22-alpine AS frontend
# pnpm at the version package.json's packageManager pins, through Node's own corepack.
RUN corepack enable
WORKDIR /source/SlideWorks.Client
COPY SlideWorks.Client/package.json SlideWorks.Client/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY SlideWorks.Client/ ./
# The shared contracts (screen JSON, sample deck) the client imports as @contracts/*.
COPY contracts/ ../contracts/
RUN pnpm run build

# Stage 2: publish the API with the SPA already inside wwwroot, so the
# published output is the complete app — no copy step in the runtime stage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Stamped into AssemblyInformationalVersion ("1.0.0+<sha>") so /health names the
# running build. docker-publish.yml passes the commit; a local build says "unknown".
ARG SOURCE_COMMIT=unknown
WORKDIR /source
COPY . .
COPY --from=frontend /source/SlideWorks.WebApi/wwwroot/ ./SlideWorks.WebApi/wwwroot/
# Analyzers off here: the root .editorconfig that tunes them is outside this build
# context, and CI already builds every project with them on and warnings as errors.
RUN dotnet publish SlideWorks.WebApi -c Release -o /app -p:SourceRevisionId=$SOURCE_COMMIT -p:RunAnalyzers=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl: the HEALTHCHECK below calls GET /health with it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# SQLite lives here (SLIDEWORKS_DB_PATH defaults to /data/slide-works.db). The compose
# file mounts a named volume on the WSL2 ext4 disk; never bind-mount this onto a
# 9p/SMB/NAS path. Created owned by the non-root app user so a fresh named volume
# inherits that ownership.
RUN mkdir -p /data && chown "$APP_UID" /data
VOLUME /data

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=60s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "SlideWorks.WebApi.dll"]
