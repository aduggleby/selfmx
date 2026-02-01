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
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS backend-build

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
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS production

WORKDIR /app

# Create non-root user
RUN addgroup -g 1001 -S selfmx && \
    adduser -S selfmx -u 1001 -G selfmx

# Create data directory
RUN mkdir -p /app/data && chown -R selfmx:selfmx /app/data

# Install wget for health checks and ICU libraries for SQL Server globalization
RUN apk add --no-cache wget icu-libs

# Copy published backend
COPY --from=backend-build --chown=selfmx:selfmx /app/publish ./

# Copy built frontend to wwwroot
COPY --from=frontend-build --chown=selfmx:selfmx /app/client/dist ./wwwroot

# Copy health check script
COPY --chown=selfmx:selfmx scripts/healthcheck.sh /app/healthcheck.sh
RUN chmod +x /app/healthcheck.sh

# Environment configuration
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Switch to non-root user
USER selfmx

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD /app/healthcheck.sh

# Start application
ENTRYPOINT ["dotnet", "SelfMX.Api.dll"]
