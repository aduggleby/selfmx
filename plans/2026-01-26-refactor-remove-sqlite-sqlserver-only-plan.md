---
title: "refactor: Remove SQLite Support, SQL Server Only"
type: refactor
date: 2026-01-26
deepened: 2026-01-26
---

# refactor: Remove SQLite Support, SQL Server Only

## Enhancement Summary

**Deepened on:** 2026-01-26
**Research agents used:** architecture-strategist, security-sentinel, performance-oracle, code-simplicity-reviewer, data-integrity-guardian, deployment-verification-agent, best-practices-researcher, framework-docs-researcher, Context7 (EF Core, Hangfire, SqlClient)

### Key Improvements
1. **Security hardening:** Added recommendations to create dedicated database user instead of using SA account
2. **Performance optimization:** Identified connection pooling configuration and Hangfire worker count tuning
3. **Simplification opportunities:** Removed unnecessary deprecation warnings, simplified password generation
4. **Data integrity:** Added transaction boundary recommendations and backup verification
5. **Docker best practices:** Added ulimits, shm_size, and memory configuration recommendations

### Critical Issues Discovered
- **Security:** SA account usage exposes full database control; create dedicated app user
- **Performance:** 2GB memory limit is at SQL Server minimum; consider 4GB for production
- **Data Integrity:** Domain deletion lacks distributed transaction for SES/Cloudflare operations
- **Simplification:** Deprecation warnings and RAM preflight check are unnecessary complexity

---

## Overview

Remove SQLite as a database option from SelfMX, making SQL Server the only supported database. Consolidate the three separate connection strings (DefaultConnection, AuditConnection, HangfireConnection) into a single DefaultConnection. Update the install script to deploy a SQL Server container alongside the SelfMX application container, connected via a private Docker network with persistent storage.

**Breaking Change:** Existing SQLite users must manually export their data before upgrading. No automated migration path will be provided.

## Problem Statement

The current multi-database provider architecture adds complexity:

1. **Code Complexity:** `Program.cs` contains ~80 lines of conditional logic for SQLite vs SQL Server paths (lines 26-107)
2. **Configuration Burden:** Users must understand and configure 3 separate connection strings
3. **Maintenance Overhead:** Two Hangfire storage implementations, two DbContext configurations, provider detection logic
4. **Install Script Divergence:** Separate docker-compose files for SQLite and SQL Server deployments
5. **Performance Limitations:** SQLite requires single Hangfire worker, separate audit database file to avoid lock contention

SQL Server eliminates these issues - it supports concurrent connections, shared database for all components, and better production scalability.

## Proposed Solution

### Architecture Changes

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Private Network                    │
│                      (selfmx-network)                        │
│                                                              │
│  ┌─────────────────┐         ┌─────────────────────────┐    │
│  │   SQL Server    │◄───────►│       SelfMX API        │    │
│  │    Container    │         │       Container         │    │
│  │                 │         │                         │    │
│  │ Port: 1433      │         │ Port: 5000 (internal)   │    │
│  │ (internal only) │         │       8080 (mapped)     │    │
│  └────────┬────────┘         └─────────────────────────┘    │
│           │                                                  │
│           ▼                                                  │
│  ┌─────────────────┐                                        │
│  │  Volume Mount   │                                        │
│  │ /data/sqlserver │                                        │
│  │   (host path)   │                                        │
│  └─────────────────┘                                        │
└─────────────────────────────────────────────────────────────┘
```

### Research Insights: Architecture

**Architectural Review (APPROVED):**
- Container network isolation is sound - private bridge network reduces attack surface
- Single database for all components is appropriate for SQL Server (no lock contention issues)
- Connection string consolidation aligns with existing fallback patterns in code

**Recommendations:**
- Standardize network name to `selfmx-net` (matches existing compose file)
- Consider `internal: true` Docker network option for maximum isolation

### Connection String Consolidation

**Before (3 connections):**
```json
{
  "Database": { "Provider": "sqlserver" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=SelfMX;...",
    "AuditConnection": "Server=...;Database=SelfMX;...",
    "HangfireConnection": "Server=...;Database=SelfMX;..."
  }
}
```

**After (1 connection):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=SelfMX;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true;Max Pool Size=200;Min Pool Size=20"
  }
}
```

### Research Insights: Connection String

**Best Practices (from Microsoft.Data.SqlClient docs):**
- `Encrypt=True` is now the default in SqlClient 4.0+ (good for security)
- `TrustServerCertificate=True` is acceptable for Docker internal networks but vulnerable to MITM on exposed networks
- Add explicit pool sizes: `Max Pool Size=200;Min Pool Size=20` to handle concurrent connections

**Performance Considerations:**
- Consider disabling `MultipleActiveResultSets=false` if not needed - reduces memory per connection
- Add `Connect Timeout=30` to prevent indefinite connection waits

### Install Script Changes

The install script will:
1. Generate a compliant SA password (simplified: `openssl rand -base64 24`)
2. Create a private Docker network (`selfmx-net`)
3. Deploy SQL Server container with:
   - 4GB memory limit (increased from 2GB based on performance research)
   - Persistent volume mount at `/data/selfmx/sqlserver`
   - Health check using `sqlcmd`
4. Deploy SelfMX container connected to same network
5. Configure Caddy for HTTPS termination

## Technical Approach

### Architecture

**Database:** Single SQL Server 2022 instance (Developer Edition)
- All tables (Domains, ApiKeys, ApiKeyDomains, AuditLogs) in one database
- Hangfire tables in same database
- Connection resilience with automatic retry (5 retries, 30s max delay)

**Container Networking:**
- Private bridge network (`selfmx-net`)
- SQL Server port 1433 NOT exposed to host (security)
- SelfMX connects via container hostname `sqlserver`

**Persistent Storage:**
- Host bind mount: `/data/selfmx/sqlserver` → container `/var/opt/mssql`
- Survives container restarts and upgrades

### Research Insights: SQL Server Docker Best Practices

**Memory Configuration:**
```yaml
deploy:
  resources:
    limits:
      memory: 4G  # Increased from 2GB - SQL Server minimum is 2GB
    reservations:
      memory: 2G
environment:
  - MSSQL_MEMORY_LIMIT_MB=3072  # Leave 1GB for OS
```

**Required Container Settings:**
```yaml
ulimits:
  nofile:
    soft: 65535
    hard: 65535
shm_size: 1g  # Required for VDI backup support
```

**Volume Permissions:**
```bash
# SQL Server runs as UID 10001
sudo chown -R 10001:0 /data/selfmx/sqlserver
sudo chmod -R 700 /data/selfmx/sqlserver
```

### Implementation Phases

#### Phase 1: Code Simplification

Remove SQLite code paths and consolidate connection string handling.

**Tasks:**
- [x] Remove `Database:Provider` configuration option from `Program.cs:26-29`
- [x] Remove SQLite DbContext configuration branch (`Program.cs:74-107`)
- [x] Remove SQLite Hangfire configuration
- [x] Update all 3 DbContext registrations to use single `DefaultConnection`
- [x] Remove SQLite WAL mode initialization (`Program.cs:238-241`)
- [x] Remove SQLite NuGet packages from `SelfMX.Api.csproj`
- [x] **DELETE** `DataMigrationService.cs` and `MigrationEndpoints.cs` (migration not supported)

### Research Insights: Simplification

**Code to Remove (per simplicity review):**
| File | Reason | LOC Saved |
|------|--------|-----------|
| `DataMigrationService.cs` | Migration explicitly not supported | 389 |
| `MigrationEndpoints.cs` | Migration explicitly not supported | ~50 |
| `Program.cs:26-29` | Provider detection | 4 |
| `Program.cs:74-107` | SQLite branch | 34 |
| `Program.cs:237-242` | SQLite WAL mode | 6 |

**Total: ~490 lines removed**

**SKIP These (unnecessary complexity):**
- ~~Deprecation warnings for legacy connection strings~~ - Breaking change is documented; fail fast or ignore silently
- ~~RAM preflight check~~ - Docker handles resource limits; let it fail with clear error

**Files to modify:**

### Program.cs

```csharp
// REMOVE: Lines 26-29 (provider detection)
// REMOVE: Lines 74-107 (entire SQLite branch)

// SIMPLIFIED: Single connection string, no provider detection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");

// DbContext registration with explicit pool configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sql.CommandTimeout(30);
    }));

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sql.CommandTimeout(30);
    }));
```

### Research Insights: EF Core Configuration

**From Context7 EF Core docs:**
- `EnableRetryOnFailure()` is required for SQL Server (not automatic like Azure SQL)
- Multiple DbContexts sharing same connection string is supported - pool is managed at ADO.NET level
- Use typed `DbContextOptions<T>` constructor for each context

**Connection Resiliency Pattern:**
```csharp
// For transactions with retry, wrap in execution strategy
var executionStrategy = dbContext.Database.CreateExecutionStrategy();
await executionStrategy.ExecuteAsync(async () =>
{
    using var transaction = await dbContext.Database.BeginTransactionAsync();
    // operations
    await transaction.CommitAsync();
});
```

### Hangfire Configuration

```csharp
// Simplified Hangfire registration
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,  // Requires Service Broker
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    // Bounded worker count for predictable resource usage
    options.WorkerCount = Math.Clamp(Environment.ProcessorCount * 2, minValue: 5, maxValue: 20);
    options.Queues = ["default"];
});
```

### Research Insights: Hangfire Performance

**From Context7 Hangfire docs:**
- `QueuePollInterval = TimeSpan.Zero` enables long polling (requires Service Broker)
- If Service Broker not enabled, use `TimeSpan.FromSeconds(5)` instead
- `DisableGlobalLocks = true` requires Schema 7 migration (automatic with `PrepareSchemaIfNecessary = true`)

**Worker Count Issue:**
- Current formula `ProcessorCount * 2` scales unpredictably (2-64 workers)
- Cap at 20 max to prevent connection pool exhaustion
- Email sending is I/O-bound, not CPU-bound

### SelfMX.Api.csproj

```xml
<!-- REMOVE these packages -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
<PackageReference Include="Hangfire.Storage.SQLite" Version="0.4.2" />

<!-- KEEP these packages -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
<PackageReference Include="Hangfire.SqlServer" Version="1.8.17" />
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SelfMX;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Max Pool Size=200;Min Pool Size=20"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Success Criteria:**
- Application starts with only `DefaultConnection` configured
- All tests pass
- No SQLite references in compiled output

---

#### Phase 2: Install Script Overhaul

Rewrite `deploy/install.sh` to deploy SQL Server container.

**Tasks:**
- [x] Simplify SA password generation (use openssl)
- [x] Create private Docker network (`selfmx-net`)
- [x] Generate docker-compose.yml with SQL Server and SelfMX services
- [x] Add SQL Server health check wait logic
- [x] Update environment variable generation
- [x] Remove SQLite-related configuration

**New docker-compose.yml template:**

### deploy/install.sh (docker-compose generation)

```yaml
version: '3.8'

networks:
  selfmx-net:
    driver: bridge

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: selfmx-sqlserver
    hostname: sqlserver
    restart: unless-stopped
    networks:
      - selfmx-net
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}
      - MSSQL_PID=Developer
      - MSSQL_MEMORY_LIMIT_MB=3072
    volumes:
      - /data/selfmx/sqlserver:/var/opt/mssql
      - /data/selfmx/backups:/var/opt/mssql/backup
    deploy:
      resources:
        limits:
          memory: 4G
        reservations:
          memory: 2G
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$${MSSQL_SA_PASSWORD}" -Q "SELECT 1" -C -N -b
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    ulimits:
      nofile:
        soft: 65535
        hard: 65535
    shm_size: 1g

  selfmx:
    image: ghcr.io/alexsawyer/selfmx:${SELFMX_VERSION:-latest}
    container_name: selfmx-api
    restart: unless-stopped
    networks:
      - selfmx-net
    depends_on:
      sqlserver:
        condition: service_healthy
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=SelfMX;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true;Max Pool Size=200;Min Pool Size=20
      - App__ApiKeyHash=${API_KEY_HASH}
      - Aws__Region=${AWS_REGION}
      - Aws__AccessKeyId=${AWS_ACCESS_KEY_ID}
      - Aws__SecretAccessKey=${AWS_SECRET_ACCESS_KEY}
      - Cloudflare__ApiToken=${CLOUDFLARE_API_TOKEN}
      - Cloudflare__ZoneId=${CLOUDFLARE_ZONE_ID}
    volumes:
      - /data/selfmx/logs:/app/logs

  caddy:
    image: caddy:2-alpine
    container_name: selfmx-caddy
    restart: unless-stopped
    networks:
      - selfmx-net
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - /data/selfmx/caddy/data:/data
      - /data/selfmx/caddy/config:/config
```

### Research Insights: Docker Configuration

**Added based on best practices research:**
- `MSSQL_MEMORY_LIMIT_MB=3072` - Prevents SQL Server from consuming all container memory
- `ulimits.nofile` - Prevents "too many open files" errors
- `shm_size: 1g` - Required for VDI backup support (default 64MB is insufficient)
- Separate backup volume mounted at `/var/opt/mssql/backup`

**SA Password Generation (SIMPLIFIED):**

### deploy/install.sh (password generation function)

```bash
generate_sa_password() {
    # Simple, secure password generation
    # Append A1! to guarantee complexity requirements
    openssl rand -base64 24 | tr -d '/+=' | head -c 28
    echo "A1!"  # Guarantees uppercase, number, special char
}
```

**Why simplified:** Original 20-line function with character class logic is over-engineered. `openssl rand` provides cryptographic randomness; appending `A1!` guarantees SQL Server complexity requirements.

**Success Criteria:**
- Fresh install completes successfully on a 4GB RAM VPS
- SQL Server container starts and becomes healthy
- SelfMX connects and creates schema automatically
- Data persists across container restarts

---

#### Phase 3: Backup/Restore Scripts

Rewrite backup and restore scripts for SQL Server.

**Tasks:**
- [x] Rewrite `selfmx-backup` to use `sqlcmd BACKUP DATABASE`
- [x] Rewrite `selfmx-restore` to use `sqlcmd RESTORE DATABASE`
- [x] Add backup integrity verification with `RESTORE VERIFYONLY`
- [x] Mount backup volume directly (no copy in/out)

### Research Insights: Backup Strategy

**From SQL Server Docker best practices:**
- Mount `/data/selfmx/backups` directly into SQL Server container at `/var/opt/mssql/backup`
- Use `BACKUP DATABASE ... WITH COMPRESSION, CHECKSUM` for integrity
- Verify backups with `RESTORE VERIFYONLY`
- Consider 3-2-1 backup strategy for production

**Backup Script (SIMPLIFIED):**

### deploy/selfmx-backup

```bash
#!/bin/bash
set -euo pipefail

# SelfMX SQL Server Backup Script
source /data/selfmx/.env
BACKUP_FILE="/var/opt/mssql/backup/selfmx_$(date +%Y%m%d_%H%M%S).bak"

echo "Starting backup..."

# Backup directly to mounted volume (no copy needed)
docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N \
    -Q "BACKUP DATABASE [SelfMX] TO DISK = N'$BACKUP_FILE' WITH FORMAT, COMPRESSION, CHECKSUM"

# Verify backup integrity
docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N \
    -Q "RESTORE VERIFYONLY FROM DISK = N'$BACKUP_FILE'"

# Cleanup old backups (7 day retention)
find /data/selfmx/backups -name "selfmx_*.bak" -mtime +7 -delete

echo "Backup complete: $BACKUP_FILE"
```

**Why simplified:** Original 50-line script created backup inside container, copied out, then deleted inside - three operations instead of one. With mounted volume, backup writes directly to host filesystem.

**Restore Script (SIMPLIFIED):**

### deploy/selfmx-restore

```bash
#!/bin/bash
set -euo pipefail

[ $# -lt 1 ] && { echo "Usage: $0 <backup-file.bak>"; exit 1; }
source /data/selfmx/.env

echo "WARNING: This replaces ALL data. Continue? (yes/no)"
read -r confirm; [ "$confirm" != "yes" ] && exit 0

docker stop selfmx-api || true

docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N \
    -Q "RESTORE DATABASE [SelfMX] FROM DISK = N'$1' WITH REPLACE"

docker start selfmx-api
echo "Restored from $1"
```

**Success Criteria:**
- `selfmx-backup` creates valid `.bak` file with checksum
- `selfmx-restore` successfully restores from backup
- Round-trip backup/restore preserves all data

---

#### Phase 4: Development Experience

Create development environment setup for local SQL Server.

**Tasks:**
- [x] Create `docker-compose.dev.yml` for local development
- [x] Update `appsettings.Development.json` with local connection string
- [x] Update test project configuration
- [x] Verify tests pass with SQL Server

### docker-compose.dev.yml

```yaml
# Development SQL Server for local SelfMX development
# Usage: docker compose -f docker-compose.dev.yml up -d

version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: selfmx-sqlserver-dev
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=Dev@Password123!
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-dev-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Dev@Password123!" -Q "SELECT 1" -C -N -b
      interval: 10s
      timeout: 5s
      retries: 10

volumes:
  sqlserver-dev-data:
```

### src/SelfMX.Api/appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SelfMX_Dev;User Id=sa;Password=Dev@Password123!;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

**Success Criteria:**
- Developer can start SQL Server with single command
- `dotnet run` connects to local SQL Server
- All tests pass against SQL Server

---

#### Phase 5: Documentation

Update all documentation to reflect SQL Server-only architecture.

**Tasks:**
- [x] Update `README.md` - remove SQLite references
- [x] Update `CLAUDE.md` - update database documentation
- [ ] Update website documentation pages
- [x] **DELETE** migration documentation (DataMigrationService removed)

### Files to Update

| File | Changes |
|------|---------|
| `README.md` | Remove SQLite references, update quick start |
| `CLAUDE.md` | Remove SQLite from database configuration table |
| `website/src/pages/configuration/database.md` | Remove PostgreSQL/SQLite, SQL Server only |
| `website/src/pages/getting-started/installation.md` | Update install instructions |
| `website/src/pages/guides/deployment.md` | Update Docker deployment guide |

**Success Criteria:**
- No SQLite references in documentation
- Installation instructions work for new users
- Existing users understand this is a breaking change

---

## Security Considerations

### Research Insights: Security Audit

**CRITICAL: SA Account Usage**

The plan uses SQL Server's SA (sysadmin) account for application access. This grants unrestricted database permissions.

**Risk:** If application is compromised, attacker gains full SQL Server control.

**Recommendation (future enhancement):**
```sql
-- Create dedicated application user with minimal permissions
CREATE LOGIN [selfmx_app] WITH PASSWORD = 'generated_secure_password';
CREATE USER [selfmx_app] FOR LOGIN [selfmx_app];
ALTER ROLE db_datareader ADD MEMBER [selfmx_app];
ALTER ROLE db_datawriter ADD MEMBER [selfmx_app];
GRANT EXECUTE TO [selfmx_app];
```

**TrustServerCertificate=True**

Acceptable for Docker internal networks (traffic stays within private network). For production with exposed ports, use proper TLS certificates.

**Health Check Password Exposure**

SA password appears in `docker inspect` output. Consider TCP port check instead:
```yaml
healthcheck:
  test: ["CMD-SHELL", "echo | nc -z localhost 1433 || exit 1"]
```

---

## Data Integrity Considerations

### Research Insights: Data Integrity

**Issue: Domain Deletion Lacks Transaction**

Current `DeleteDomain` endpoint deletes from SES, Cloudflare, then database. If Cloudflare succeeds but database fails, system enters inconsistent state.

**Recommendation:**
```csharp
var executionStrategy = dbContext.Database.CreateExecutionStrategy();
await executionStrategy.ExecuteAsync(async () =>
{
    using var transaction = await dbContext.Database.BeginTransactionAsync();
    await domainService.DeleteAsync(domain);
    await sesService.DeleteDomainIdentityAsync(domain.Name);
    await cloudflareService.DeleteDnsRecordsForDomainAsync(domain.Name);
    await transaction.CommitAsync();
});
```

**Issue: EnsureCreatedAsync with Multiple DbContexts**

Calling `EnsureCreatedAsync()` on multiple DbContexts against same database may leave schema incomplete.

**Recommendation:** Use EF Core Migrations for production, or consolidate to single DbContext.

---

## Performance Optimization

### Research Insights: Performance

**Memory Allocation:**
- 2GB is SQL Server minimum; 4GB recommended for moderate workloads
- Configure `MSSQL_MEMORY_LIMIT_MB` to prevent SQL Server consuming all container memory

**Connection Pooling:**
```
Current: Default (Max=100, Min=0)
Recommended: Max Pool Size=200;Min Pool Size=20
```

With 16 Hangfire workers + web requests, can exceed default 100 connection limit.

**Hangfire Worker Count:**
```csharp
// Current: Environment.ProcessorCount * 2 (unbounded)
// Recommended: Bounded between 5-20
options.WorkerCount = Math.Clamp(Environment.ProcessorCount * 2, 5, 20);
```

Email sending is I/O-bound; too many workers exhaust connection pool.

---

## Alternative Approaches Considered

### 1. Keep SQLite for Development

**Rejected:** Creates dev/prod divergence, "works on my machine" issues.

### 2. Support Multiple Providers via Abstraction

**Rejected:** Over-engineering for self-hosted tool.

### 3. Use PostgreSQL Instead

**Rejected:** SQL Server already implemented and tested.

### 4. Provide Automated Migration from SQLite

**Rejected:** User chose clean break; reduces complexity.

---

## Acceptance Criteria

### Functional Requirements

- [ ] Application starts with only `DefaultConnection` configured
- [ ] AppDbContext, AuditDbContext, and Hangfire share single database
- [ ] Install script deploys SQL Server container on private network
- [ ] SQL Server data persists at `/data/selfmx/sqlserver`
- [ ] SQL Server port (1433) is NOT exposed to host
- [ ] Backup script creates valid `.bak` files with checksum verification
- [ ] Restore script successfully restores from backup

### Non-Functional Requirements

- [ ] SQL Server container uses maximum 4GB RAM (increased from 2GB)
- [ ] Application handles SQL Server connection failures with retry
- [ ] Backup completes in under 5 minutes for typical data sizes
- [ ] Fresh install completes in under 10 minutes

### Quality Gates

- [ ] All existing tests pass
- [ ] No SQLite packages in final build
- [ ] No SQLite code paths in Program.cs
- [ ] DataMigrationService.cs deleted
- [ ] Documentation updated with no SQLite references
- [ ] Install script tested on Ubuntu 22.04 and Debian 12

---

## Deployment Verification Checklist

### Pre-Deploy (Red - Stop if Any Fail)

- [ ] Docker and Docker Compose installed
- [ ] Disk space >= 5GB at `/data/selfmx`
- [ ] No existing container named `selfmx-sqlserver`

### Deploy

```bash
curl -sSL https://selfmx.example.com/install.sh | bash
```

### Post-Deploy (Green - Verify Success)

```sql
-- Verify schema created
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo';

-- Verify Hangfire schema
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'HangFire';

-- Verify indexes exist
SELECT t.name, i.name FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('Domains', 'ApiKeys', 'AuditLogs');
```

### Rollback

If SQL Server fails to start:
1. Check logs: `docker logs selfmx-sqlserver`
2. Verify memory: `free -h`
3. Verify disk: `df -h /data/selfmx`

---

## Success Metrics

1. **Code Reduction:** Remove ~490 lines (SQLite code + DataMigrationService)
2. **Package Reduction:** Remove 2 NuGet packages (EF Core SQLite, Hangfire SQLite)
3. **Configuration Simplification:** 3 connection strings → 1 connection string
4. **Install Script:** Single docker-compose.yml instead of SQLite + SQL Server variants

---

## Dependencies & Prerequisites

### External Dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| mcr.microsoft.com/mssql/server | 2022-latest | Database container |
| Hangfire.SqlServer | 1.8.17 | Background job storage |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.0 | ORM for SQL Server |

---

## Risk Analysis & Mitigation

### Risk 1: Breaking Existing SQLite Users

**Severity:** HIGH | **Likelihood:** CERTAIN

**Mitigation:**
- Document breaking change in release notes
- Version bump: 0.9.x → 1.0.0 (semver major)

### Risk 2: SQL Server Memory Exhaustion

**Severity:** MEDIUM | **Likelihood:** LOW

**Mitigation:**
- Set memory limit to 4GB (increased from 2GB)
- Configure `MSSQL_MEMORY_LIMIT_MB=3072`

### Risk 3: Connection Pool Exhaustion

**Severity:** MEDIUM | **Likelihood:** MEDIUM

**Mitigation:**
- Configure `Max Pool Size=200;Min Pool Size=20`
- Cap Hangfire workers at 20

---

## Resource Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| RAM | 4GB | 8GB+ |
| Disk | 10GB | 20GB+ |
| CPU | 2 cores | 4 cores |

---

## References & Research

### External Documentation
- [SQL Server Docker Best Practices](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment)
- [EF Core Connection Resiliency](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency)
- [Hangfire SQL Server Storage](https://docs.hangfire.io/en/latest/configuration/using-sql-server.html)
- [Microsoft.Data.SqlClient Connection Strings](https://github.com/dotnet/sqlclient)

### Research Agent Reports
- Architecture Strategist: APPROVED with recommendations
- Security Sentinel: SA account usage flagged; TrustServerCertificate acceptable for internal networks
- Performance Oracle: Memory and connection pooling optimizations identified
- Code Simplicity Reviewer: ~490 LOC reduction; simplified password generation and backup scripts
- Data Integrity Guardian: Transaction boundaries needed for distributed operations

---

## Checklist Summary

### Phase 1: Code Simplification
- [ ] Remove Database:Provider config
- [ ] Remove SQLite branch from Program.cs
- [ ] Consolidate to single DefaultConnection
- [ ] Remove SQLite NuGet packages
- [ ] **DELETE** DataMigrationService.cs
- [ ] **DELETE** MigrationEndpoints.cs

### Phase 2: Install Script
- [ ] Simplify SA password generation (openssl)
- [ ] Generate SQL Server docker-compose with 4GB memory
- [ ] Add ulimits and shm_size
- [ ] Create private Docker network (`selfmx-net`)

### Phase 3: Backup/Restore
- [ ] Simplified selfmx-backup with direct volume mount
- [ ] Add RESTORE VERIFYONLY for backup verification
- [ ] Simplified selfmx-restore

### Phase 4: Development
- [ ] Create docker-compose.dev.yml
- [ ] Update appsettings.Development.json
- [ ] Verify all tests pass

### Phase 5: Documentation
- [ ] Update README.md
- [ ] Update CLAUDE.md
- [ ] Delete migration docs
- [ ] Add CHANGELOG entry for breaking change
