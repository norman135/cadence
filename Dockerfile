# syntax=docker/dockerfile:1
#
# Cadence application image: the ASP.NET Core API serving the compiled React app, plus the
# one-shot database migrator (/app/migrator).
#
# Multi-arch without emulation: the build stages run on the build machine's native platform
# ($BUILDPLATFORM) and cross-compile for the target ($TARGETARCH). The final stage only copies
# files, so building linux/arm64 on an amd64 machine (or vice versa) needs no QEMU.
#
#   docker build -t cadence:local --build-arg VERSION=0.1.0 .

ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=22

# ---------------------------------------------------------------------------------------------
# Frontend: static assets are platform-independent, so they are built once, natively.
# ---------------------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM node:${NODE_VERSION}-alpine AS web
WORKDIR /src/web

COPY web/package.json web/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund

COPY web/ ./
RUN npm run build

# ---------------------------------------------------------------------------------------------
# Backend: restore from project files first so dependency layers are cached across code changes.
# ---------------------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS api
ARG TARGETARCH
ARG VERSION=0.0.0-local
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Cadence.Domain/Cadence.Domain.csproj src/Cadence.Domain/
COPY src/Cadence.Application/Cadence.Application.csproj src/Cadence.Application/
COPY src/Cadence.Infrastructure/Cadence.Infrastructure.csproj src/Cadence.Infrastructure/
COPY src/Cadence.ServiceDefaults/Cadence.ServiceDefaults.csproj src/Cadence.ServiceDefaults/
COPY src/Cadence.Api/Cadence.Api.csproj src/Cadence.Api/
COPY src/Cadence.Migrator/Cadence.Migrator.csproj src/Cadence.Migrator/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Cadence.Api/Cadence.Api.csproj -a $TARGETARCH -p:PublishReadyToRun=true \
 && dotnet restore src/Cadence.Migrator/Cadence.Migrator.csproj -a $TARGETARCH -p:PublishReadyToRun=true

COPY src/ src/
COPY --from=web /src/web/dist/ src/Cadence.Api/wwwroot/

# ReadyToRun precompiles to native code: faster startup and less JIT memory at runtime.
# Analyzers and OpenAPI generation already ran in CI, so they are skipped here.
ARG PUBLISH_FLAGS="-c Release --no-restore -p:PublishReadyToRun=true -p:RunAnalyzers=false -p:OpenApiGenerateDocuments=false"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Cadence.Api/Cadence.Api.csproj -a $TARGETARCH $PUBLISH_FLAGS \
      -p:MinVerVersionOverride=$VERSION -o /out/app \
 && dotnet publish src/Cadence.Migrator/Cadence.Migrator.csproj -a $TARGETARCH $PUBLISH_FLAGS \
      -p:MinVerVersionOverride=$VERSION -o /out/app/migrator \
 && mkdir -p /out/data/keys

# ---------------------------------------------------------------------------------------------
# Runtime: Ubuntu chiseled ASP.NET image with no shell or package manager, running as a non-root
# user (UID 1654). Small attack surface and a small image.
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled AS final
WORKDIR /app

# A static busybox provides `wget` for container health checks (the chiseled image has no shell or
# curl). It adds about 1 MB instead of starting a second .NET process inside the memory limit.
COPY --from=busybox:1.37-musl /bin/wget /usr/local/bin/wget

COPY --from=api /out/app ./
COPY --from=api --chown=1654:1654 /out/data /app/data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Cadence__DataProtection__KeysDirectory=/app/data/keys

EXPOSE 8080
USER 1654

HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD ["/usr/local/bin/wget", "-q", "--spider", "http://127.0.0.1:8080/health/live"]

ENTRYPOINT ["dotnet", "Cadence.Api.dll"]
