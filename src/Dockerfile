# Build context is src/: docker build -t slide-works src

# Stage 1: build the SPA straight into the API project's wwwroot.
FROM node:22-alpine AS frontend
# pnpm at the version the workspace root's package.json (packageManager) pins, through Node's own corepack.
RUN corepack enable
WORKDIR /source
# The pnpm workspace root (one lockfile for every package in src/), the SPA's manifest and the whole of
# @slideworks/core (it ships its .ts sources, no build step). The install takes only the SPA and the workspace
# packages it depends on, so another package in the workspace (the app) needs nothing copied here.
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY SlideWorks.Client/package.json SlideWorks.Client/
COPY SlideWorks.Core.Client/ SlideWorks.Core.Client/
RUN pnpm install --frozen-lockfile --filter slide-works-client...
COPY SlideWorks.Client/ SlideWorks.Client/
# The shared contracts (screen JSON, sample deck) the client imports as @contracts/*.
COPY contracts/ contracts/
RUN pnpm --dir SlideWorks.Client run build

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
# The Ubuntu mirrors sometimes answer one request with a 5xx (an HTTP 520 on archive.ubuntu.com's InRelease failed a CI
# build). apt retries each failed download, here and in the Playwright --with-deps install below, and both apt steps
# rerun whole up to 3 times, since a failed InRelease fails apt-get update outright.
RUN printf 'Acquire::Retries "5";\nAcquire::http::Timeout "30";\n' > /etc/apt/apt.conf.d/80-retries
# curl: the HEALTHCHECK below calls GET /health with it.
RUN for i in 1 2 3; do \
        apt-get update && apt-get install -y --no-install-recommends curl && break; \
        [ "$i" = 3 ] && exit 1; sleep 15; \
    done \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Headless Chromium for the share card (L9), installed as root with its system libraries by the Playwright driver that
# Microsoft.Playwright publishes into .playwright/ (its own node; this image has no pwsh). Only the headless shell
# (--only-shell, SW-155): RenderBrowser launches Playwright's default headless Chromium, which is the shell, and the full
# browser would add about 390 MB. It goes in a shared folder
# the non-root app user can read, and the app finds it through PLAYWRIGHT_BROWSERS_PATH. The driver in .playwright/
# is opened up too: the app (non-root) starts its node at runtime, and the publish leaves it 0744 and root's.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN for i in 1 2 3; do \
        .playwright/node/linux-x64/node .playwright/package/cli.js install --with-deps --only-shell chromium && break; \
        [ "$i" = 3 ] && exit 1; sleep 15; \
    done \
    && chmod -R a+rX "$PLAYWRIGHT_BROWSERS_PATH" .playwright \
    && rm -rf /var/lib/apt/lists/*

# SQLite lives here (SLIDEWORKS_DB_PATH defaults to /data/slide-works.db). The compose
# file mounts a named volume on a local disk; never bind-mount this onto a
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
