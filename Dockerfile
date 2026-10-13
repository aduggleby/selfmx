# SelfMX Dockerfile - Multi-stage build
# Builds both frontend (React/Vite) and backend (ASP.NET Core)

# ============================================
# Stage 1: Build Frontend
# ============================================
FROM node:22-alpine AS frontend-build

WORKDIR /app/client

# Copy package files first for layer caching
COPY client/package*.json ./
RUN npm ci --no-audit --no-fund

# Copy source and build
COPY client/ ./
RUN npm run build

# ============================================
# Stage 2: Build Backend
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build

WORKDIR /src

# Copy solution and project files for restore
COPY SelfMX.slnx ./
COPY src/SelfMX.Api/SelfMX.Api.csproj src/SelfMX.Api/

# Restore dependencies
RUN dotnet restore src/SelfMX.Api/SelfMX.Api.csproj

# Copy source and build
COPY src/ src/
RUN dotnet publish src/SelfMX.Api/SelfMX.Api.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false

# ============================================
# Stage 3: Production Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS production

WORKDIR /app

# Run as the base image's non-root "app" user (UID 1654). Writable paths for
# hardened read-only containers: DataProtection keys and log files.
RUN mkdir -p /var/lib/selfmx/keys /var/log/selfmx && \
    chown -R app:app /var/lib/selfmx /var/log/selfmx

# wget for health checks, ICU for SQL Server globalization, krb5-libs for Npgsql GSS probing
RUN apk add --no-cache wget icu-libs krb5-libs

# Copy published backend
COPY --from=backend-build --chown=app:app /app/publish ./

# Copy built frontend to wwwroot
COPY --from=frontend-build --chown=app:app /app/client/dist ./wwwroot

# Copy health check script
COPY --chown=app:app scripts/healthcheck.sh /app/healthcheck.sh
RUN chmod +x /app/healthcheck.sh

# Environment configuration
# DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false overrides the Alpine base image default
# This is required for SQL Server client which needs ICU for culture support
ENV ASPNETCORE_URLS=http://+:17400 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    App__DataProtectionKeysPath=/var/lib/selfmx/keys

# Switch to non-root user
USER app

# Expose port (SelfMX reserved port range: 17400-17499)
EXPOSE 17400

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD /app/healthcheck.sh

# Start application
ENTRYPOINT ["dotnet", "SelfMX.Api.dll"]
