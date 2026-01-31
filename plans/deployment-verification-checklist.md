# Deployment Verification Checklist: SQL Server Migration

**Deployment Type:** Infrastructure change (SQLite to SQL Server)
**Risk Level:** HIGH - Database backend replacement, configuration breaking change
**Created:** 2026-01-26
**Plan Reference:** `/home/alex/Source/selfmx/plans/feat-docker-deployment-hetzner-install-script.md`

---

## Executive Summary

This deployment introduces SQL Server as the database backend for SelfMX. This is a **breaking change** that requires:

1. **Fresh installs:** No action needed - install.sh handles SQL Server setup automatically
2. **Existing SQLite installs:** Manual data export required before migration

**Key Breaking Changes:**
- Connection string format changes from SQLite path to SQL Server connection string
- 3 separate connection strings consolidated to 1 shared database
- Minimum RAM requirement increases to 3GB (SQL Server needs 2GB)

---

## Data Invariants

The following invariants MUST remain true after deployment:

| Invariant | Verification Method |
|-----------|---------------------|
| All existing domains remain accessible | Row count comparison |
| All API keys remain functional | Row count + validation test |
| API key domain scopes preserved | ApiKeyDomains row count match |
| Audit log history preserved | AuditLogs row count match |
| No NULL values in required columns | Constraint validation |
| Unique constraints enforced (Domain.Name) | Index verification |
| Foreign key integrity (ApiKeyDomains) | Relationship check |

---

## PRE-DEPLOY: Red Checklist (STOP if any fail)

### 1. Infrastructure Requirements

```bash
# 1.1 Check available RAM (MINIMUM 3GB required for SQL Server)
free -h | grep Mem
# Expected: Total >= 3Gi, Available >= 2Gi

# Alternative: Get numeric value
FREE_MEM_GB=$(free -g | awk '/Mem:/ {print $7}')
[ "$FREE_MEM_GB" -ge 2 ] && echo "RAM OK: ${FREE_MEM_GB}GB available" || echo "FAIL: Insufficient RAM"

# 1.2 Check available disk space (minimum 5GB required)
df -h / | awk 'NR==2 {print $4}'
# Expected: >= 5G

# 1.3 Check Docker is installed and running
docker --version && docker compose version
# Expected: Docker version 24.x+, Docker Compose v2.x+

systemctl is-active docker
# Expected: active

# 1.4 Check ports 80/443 are available (or custom ports)
ss -tlnp | grep -E ':80 |:443 '
# Expected: No output (ports free) OR only Caddy if updating existing install

# 1.5 Verify Docker can pull SQL Server image
docker pull mcr.microsoft.com/mssql/server:2022-latest --quiet && echo "Image pull OK"
# Expected: "Image pull OK"
```

### 2. Network Requirements

```bash
# 2.1 Verify Docker network can be created
docker network create selfmx-test-net && docker network rm selfmx-test-net
# Expected: Network created and removed successfully

# 2.2 Verify DNS resolution works (replace with your domain)
SELFMX_DOMAIN="mail.example.com"
SERVER_IP=$(curl -4 -s ifconfig.me)
DNS_IP=$(dig +short $SELFMX_DOMAIN @8.8.8.8 | head -1)
echo "Server IP: $SERVER_IP"
echo "DNS resolves to: $DNS_IP"
[ "$SERVER_IP" = "$DNS_IP" ] && echo "DNS OK" || echo "DNS MISMATCH - STOP DEPLOYMENT"
```

### 3. SQL Server Password Requirements

```bash
# SQL Server SA password MUST meet complexity requirements:
# - Minimum 8 characters
# - Contains uppercase letter
# - Contains lowercase letter
# - Contains digit
# - Contains special character

# Test password complexity (replace with your password)
MSSQL_SA_PASSWORD="YourP@ssw0rd"

validate_password() {
    local pw="$1"
    [[ ${#pw} -ge 8 ]] || { echo "FAIL: Password too short"; return 1; }
    [[ "$pw" =~ [A-Z] ]] || { echo "FAIL: Missing uppercase"; return 1; }
    [[ "$pw" =~ [a-z] ]] || { echo "FAIL: Missing lowercase"; return 1; }
    [[ "$pw" =~ [0-9] ]] || { echo "FAIL: Missing digit"; return 1; }
    [[ "$pw" =~ [\@\#\$\%\^\&\*\!\-\_\+\=] ]] || { echo "FAIL: Missing special char"; return 1; }
    echo "Password OK"
}
validate_password "$MSSQL_SA_PASSWORD"
```

---

## PRE-DEPLOY: Existing SQLite Installation Baseline

**CRITICAL:** If migrating from SQLite, save these values BEFORE any changes.

### 4. Capture SQLite Baseline Counts

```bash
# 4.1 Connect to running SelfMX container
docker exec -it selfmx-app /bin/sh

# 4.2 Record baseline counts (SAVE THESE VALUES)
echo "=== BASELINE COUNTS - $(date) ===" | tee /tmp/baseline.txt

echo "Domains:" | tee -a /tmp/baseline.txt
sqlite3 /app/data/selfmx.db "SELECT COUNT(*) FROM Domains;" | tee -a /tmp/baseline.txt

echo "ApiKeys:" | tee -a /tmp/baseline.txt
sqlite3 /app/data/selfmx.db "SELECT COUNT(*) FROM ApiKeys;" | tee -a /tmp/baseline.txt

echo "ApiKeyDomains:" | tee -a /tmp/baseline.txt
sqlite3 /app/data/selfmx.db "SELECT COUNT(*) FROM ApiKeyDomains;" | tee -a /tmp/baseline.txt

echo "AuditLogs:" | tee -a /tmp/baseline.txt
sqlite3 /app/data/audit.db "SELECT COUNT(*) FROM AuditLogs;" 2>/dev/null || echo "0" | tee -a /tmp/baseline.txt

# Record these values:
# Domains: _______
# ApiKeys: _______
# ApiKeyDomains: _______
# AuditLogs: _______

# 4.3 Verify no NULL in required fields
echo "Checking for NULL values in required fields..."
sqlite3 /app/data/selfmx.db "SELECT COUNT(*) FROM Domains WHERE Name IS NULL;"
# Expected: 0

sqlite3 /app/data/selfmx.db "SELECT COUNT(*) FROM ApiKeys WHERE KeyHash IS NULL OR KeySalt IS NULL OR KeyPrefix IS NULL;"
# Expected: 0

# 4.4 Create backup of SQLite databases
sqlite3 /app/data/selfmx.db ".backup /app/data/selfmx-pre-migration.db"
sqlite3 /app/data/audit.db ".backup /app/data/audit-pre-migration.db" 2>/dev/null

# 4.5 Export as SQL (optional, for manual recovery)
sqlite3 /app/data/selfmx.db ".dump" > /app/data/selfmx-export.sql
sqlite3 /app/data/audit.db ".dump" > /app/data/audit-export.sql 2>/dev/null

echo "Baseline captured and backups created"
exit
```

---

## DEPLOY: Installation Steps

### Scenario A: Fresh Installation

```bash
# A.1 Download and run install script
curl -fsSL https://selfmx.com/install.sh | bash

# The script will:
# - Check system requirements (including 3GB RAM)
# - Install Docker if not present
# - Prompt for configuration
# - Generate docker-compose.yml with SQL Server
# - Deploy SQL Server container (2GB memory limit)
# - Deploy SelfMX container
# - Wait for SQL Server health check
# - Configure automatic backups
```

### Scenario B: Existing SQLite Installation Migration

```bash
# B.1 Create full backup of existing installation
/usr/local/bin/selfmx-backup

# B.2 Stop existing services
cd /opt/selfmx && docker compose down

# B.3 Backup current configuration
cp .env .env.sqlite-backup
cp docker-compose.yml docker-compose.yml.sqlite-backup

# B.4 Set SQL Server password
export MSSQL_SA_PASSWORD="YourSecureP@ssw0rd123"

# B.5 Update docker-compose.yml to use SQL Server
# Option 1: Use override file
cat > docker-compose.override.yml << 'EOF'
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: selfmx-sqlserver
    hostname: sqlserver
    restart: unless-stopped
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}
      - MSSQL_PID=Developer
      - MSSQL_COLLATION=SQL_Latin1_General_CP1_CI_AS
    volumes:
      - sqlserver_data:/var/opt/mssql
    networks:
      - selfmx-net
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$${MSSQL_SA_PASSWORD}" -Q "SELECT 1" -C -N -b
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G

  selfmx:
    depends_on:
      sqlserver:
        condition: service_healthy
    environment:
      - Database__Provider=sqlserver
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=SelfMX;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true
      - ConnectionStrings__AuditConnection=Server=sqlserver;Database=SelfMX;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True
      - ConnectionStrings__HangfireConnection=Server=sqlserver;Database=SelfMX;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True

volumes:
  sqlserver_data:
EOF

# B.6 Add MSSQL_SA_PASSWORD to .env
echo "MSSQL_SA_PASSWORD=$MSSQL_SA_PASSWORD" >> .env

# B.7 Start SQL Server first
docker compose up -d sqlserver

# B.8 Wait for SQL Server to be healthy (60-90 seconds)
echo "Waiting for SQL Server to be healthy..."
for i in {1..30}; do
    STATUS=$(docker inspect selfmx-sqlserver --format='{{.State.Health.Status}}' 2>/dev/null)
    echo "Attempt $i: $STATUS"
    [ "$STATUS" = "healthy" ] && break
    sleep 5
done

# B.9 Verify SQL Server is accepting connections
docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N \
  -Q "SELECT @@VERSION"
# Expected: Microsoft SQL Server 2022...

# B.10 Start SelfMX
docker compose up -d selfmx

# B.11 Wait for SelfMX health check
echo "Waiting for SelfMX to be healthy..."
for i in {1..20}; do
    if docker exec selfmx-app wget -qO- http://127.0.0.1:5000/health 2>/dev/null; then
        echo "SelfMX is healthy!"
        break
    fi
    sleep 3
done

# B.12 Trigger migration (if SQLite data exists)
ADMIN_API_KEY="re_your_admin_key_here"
curl -X GET http://localhost:5000/v1/migration/status \
  -H "Authorization: Bearer $ADMIN_API_KEY" | jq

# If status is "pending", start migration:
curl -X POST http://localhost:5000/v1/migration/start \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_API_KEY" \
  -d '{}' | jq
```

---

## POST-DEPLOY: Yellow Checklist (Immediate Health Checks)

### 5. Container Health (Within 2 Minutes)

```bash
# 5.1 Check SQL Server container is healthy
docker inspect selfmx-sqlserver --format='{{.State.Health.Status}}'
# Expected: healthy

# 5.2 Check SelfMX container is healthy
docker inspect selfmx-app --format='{{.State.Health.Status}}'
# Expected: healthy

# 5.3 Check all containers are running
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep selfmx
# Expected: All show "Up" and "(healthy)"

# 5.4 Verify SelfMX can connect to SQL Server (check logs)
docker logs selfmx-app 2>&1 | grep -i "database\|sql\|connection" | tail -5
# Expected: No connection errors

# 5.5 Test health endpoint
curl -s http://localhost:5000/health
# Expected: {"status":"Healthy"} or similar

# 5.6 Test external access (via Caddy)
curl -s -o /dev/null -w "%{http_code}" https://$SELFMX_DOMAIN/health
# Expected: 200
```

---

## POST-DEPLOY: Green Checklist (SQL Server Verification)

### 6. SQL Server Schema Verification

```bash
# 6.1 Connect to SQL Server
docker exec -it selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N

# Run these queries (copy/paste into sqlcmd):
```

```sql
-- 6.2 Verify database exists
SELECT name FROM sys.databases WHERE name = 'SelfMX';
GO
-- Expected: SelfMX

-- 6.3 Verify all tables exist
USE SelfMX;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;
GO
-- Expected: ApiKeyDomains, ApiKeys, AuditLogs, Domains (+ Hangfire tables)

-- 6.4 Verify row counts (compare with baseline)
SELECT 'Domains' as TableName, COUNT(*) as RowCount FROM Domains
UNION ALL
SELECT 'ApiKeys', COUNT(*) FROM ApiKeys
UNION ALL
SELECT 'ApiKeyDomains', COUNT(*) FROM ApiKeyDomains
UNION ALL
SELECT 'AuditLogs', COUNT(*) FROM AuditLogs;
GO
-- Compare with pre-migration baseline!

-- 6.5 Verify indexes exist
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IS NOT NULL AND t.name IN ('Domains', 'ApiKeys', 'AuditLogs')
ORDER BY t.name, i.name;
GO
-- Expected: IX_Domains_Name, IX_ApiKeys_KeyPrefix, IX_AuditLogs_Timestamp

-- 6.6 Verify foreign key relationships
SELECT
    fk.name AS FK_Name,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) = 'ApiKeyDomains';
GO
-- Expected: FK to ApiKeys, FK to Domains

-- 6.7 Verify unique constraints
SELECT
    i.name AS IndexName,
    t.name AS TableName,
    i.is_unique
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.is_unique = 1 AND t.name = 'Domains';
GO
-- Expected: Unique index on Domains.Name

-- 6.8 Check for NULL values in required columns
SELECT 'Domains with NULL Name' as Check, COUNT(*) as Count
FROM Domains WHERE Name IS NULL
UNION ALL
SELECT 'ApiKeys with NULL KeyHash', COUNT(*)
FROM ApiKeys WHERE KeyHash IS NULL
UNION ALL
SELECT 'ApiKeys with NULL KeySalt', COUNT(*)
FROM ApiKeys WHERE KeySalt IS NULL;
GO
-- Expected: All counts = 0

-- 6.9 Check for duplicate domain names
SELECT Name, COUNT(*) as DuplicateCount
FROM Domains
GROUP BY Name
HAVING COUNT(*) > 1;
GO
-- Expected: No rows (0 duplicates)

-- Exit sqlcmd
EXIT
```

---

## POST-DEPLOY: Blue Checklist (Functional Verification)

### 7. API Functional Tests

```bash
# 7.1 Test admin login
curl -X POST https://$SELFMX_DOMAIN/v1/admin/login \
  -H "Content-Type: application/json" \
  -d '{"password": "your_admin_password"}' \
  -c cookies.txt -w "\nHTTP Status: %{http_code}\n"
# Expected: HTTP Status: 200

# 7.2 Test API key authentication
curl -X GET https://$SELFMX_DOMAIN/v1/domains \
  -H "Authorization: Bearer re_your_api_key" \
  -w "\nHTTP Status: %{http_code}\n"
# Expected: HTTP Status: 200, returns domain list

# 7.3 Verify domain count matches baseline
DOMAIN_COUNT=$(curl -s https://$SELFMX_DOMAIN/v1/domains \
  -H "Authorization: Bearer re_your_api_key" | jq '.data | length')
echo "Domain count: $DOMAIN_COUNT"
# Expected: Matches baseline count

# 7.4 Test API key listing (admin only)
curl -s https://$SELFMX_DOMAIN/v1/api-keys \
  -b cookies.txt | jq '.data | length'
# Expected: Matches baseline ApiKeys count

# 7.5 Test audit log endpoint
curl -s https://$SELFMX_DOMAIN/v1/audit \
  -b cookies.txt | jq '.data | length'
# Expected: > 0 (recent operations logged)

# 7.6 Test unauthorized access (should fail)
curl -s https://$SELFMX_DOMAIN/v1/domains \
  -w "\nHTTP Status: %{http_code}\n"
# Expected: HTTP Status: 401
```

### 8. Backup System Verification

```bash
# 8.1 Test SQL Server backup capability
docker exec selfmx-sqlserver mkdir -p /var/opt/mssql/backup

docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N \
  -Q "BACKUP DATABASE SelfMX TO DISK = '/var/opt/mssql/backup/selfmx-test.bak' WITH INIT"
# Expected: "BACKUP DATABASE successfully processed"

# 8.2 Verify backup file was created
docker exec selfmx-sqlserver ls -lh /var/opt/mssql/backup/
# Expected: selfmx-test.bak exists

# 8.3 Clean up test backup
docker exec selfmx-sqlserver rm /var/opt/mssql/backup/selfmx-test.bak
```

---

## MIGRATION VERIFICATION (For Existing Installations)

### 9. Migration Status Check

```bash
# 9.1 Check migration endpoint status
curl -s http://localhost:5000/v1/migration/status \
  -H "Authorization: Bearer $ADMIN_API_KEY" | jq
# Expected: {"state": "complete", ...}

# 9.2 Check migration state file
docker exec selfmx-app cat /app/data/.migration-state
# Expected: COMPLETE:2026-01-26T...
```

### 10. Row Count Comparison Table

| Table | Pre-Migration (SQLite) | Post-Migration (SQL Server) | Match? |
|-------|------------------------|----------------------------|--------|
| Domains | _______ | _______ | [ ] |
| ApiKeys | _______ | _______ | [ ] |
| ApiKeyDomains | _______ | _______ | [ ] |
| AuditLogs | _______ | _______ | [ ] |

**STOP AND INVESTIGATE if counts don't match before proceeding.**

---

## ROLLBACK PROCEDURES

### Scenario 1: SQL Server Container Fails to Start

**Symptoms:** Container unhealthy, memory errors, constant restarts

```bash
# Check container logs
docker logs selfmx-sqlserver --tail 100

# Common issues:
# 1. Insufficient memory - check free memory
free -h
# Fix: Increase server RAM or reduce SQL Server memory limit

# 2. Password complexity - check password requirements
# Fix: Use password meeting complexity requirements

# 3. Volume permissions
docker volume inspect selfmx_sqlserver_data
# Fix: Remove and recreate volume

# Rollback to SQLite:
cd /opt/selfmx
docker compose down
rm docker-compose.override.yml  # Remove SQL Server config
cp docker-compose.yml.sqlite-backup docker-compose.yml
docker compose up -d
```

### Scenario 2: SelfMX Can't Connect to SQL Server

**Symptoms:** SelfMX unhealthy, connection timeout errors

```bash
# 1. Verify SQL Server accepts connections
docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N -Q "SELECT 1"
# Expected: 1

# 2. Test network connectivity
docker exec selfmx-app ping -c 3 sqlserver
# Expected: 3 packets received

# 3. Check connection string
docker exec selfmx-app printenv | grep ConnectionStrings
# Verify Server=sqlserver matches container hostname

# 4. Check Docker network
docker network inspect selfmx_selfmx-net
# Both containers should be on same network

# If connection fails:
# - Verify password matches in both containers
# - Verify hostname 'sqlserver' resolves
# - Check SQL Server is listening on port 1433
```

### Scenario 3: Schema Creation Fails

**Symptoms:** Tables don't exist, EF Core errors

```bash
# 1. Check SelfMX logs
docker logs selfmx-app 2>&1 | grep -i "migration\|error\|exception"

# 2. Try manual schema creation
docker exec -it selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N

# In sqlcmd:
USE SelfMX;
SELECT * FROM sys.tables;
GO
-- If empty, restart SelfMX to trigger schema creation

# 3. Nuclear option - drop and recreate database
DROP DATABASE SelfMX;
GO
-- Then restart SelfMX container
```

### Scenario 4: Data Migration Fails

**Symptoms:** Migration endpoint returns failure, row counts don't match

```bash
# 1. Check migration state
docker exec selfmx-app cat /app/data/.migration-state
# If FAILED:..., note the error

# 2. Check which data was migrated
docker exec -it selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N -d SelfMX \
  -Q "SELECT 'Domains', COUNT(*) FROM Domains UNION ALL SELECT 'ApiKeys', COUNT(*) FROM ApiKeys"

# 3. Clear migration state and retry
docker exec selfmx-app rm /app/data/.migration-state

# 4. Truncate partially migrated data
docker exec -it selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N -d SelfMX \
  -Q "TRUNCATE TABLE ApiKeyDomains; DELETE FROM ApiKeys; DELETE FROM Domains; DELETE FROM AuditLogs;"

# 5. Restart SelfMX and retry migration
docker restart selfmx-app
curl -X POST http://localhost:5000/v1/migration/start \
  -H "Authorization: Bearer $ADMIN_API_KEY"
```

### Emergency Rollback: Full Restore to SQLite

```bash
# 1. Stop all services
cd /opt/selfmx && docker compose down

# 2. Remove SQL Server container and volume
docker rm -f selfmx-sqlserver
docker volume rm selfmx_sqlserver_data

# 3. Restore original configuration
cp docker-compose.yml.sqlite-backup docker-compose.yml
cp .env.sqlite-backup .env

# 4. Restore SQLite databases from backup
LATEST_BACKUP=$(ls -t /var/backups/selfmx/daily/*.tar.gz | head -1)
/usr/local/bin/selfmx-restore "$LATEST_BACKUP"

# 5. Verify restore
docker exec selfmx-app sqlite3 /app/data/selfmx.db "PRAGMA integrity_check;"
curl http://localhost:5000/health
```

---

## MONITORING (24 Hours)

### Metrics to Watch

| Metric | Normal Range | Alert Threshold | Check Command |
|--------|--------------|-----------------|---------------|
| SQL Server memory | 1-2GB | >2.5GB | `docker stats selfmx-sqlserver --no-stream` |
| SQL Server connections | 1-10 | >50 | Query `sys.dm_exec_sessions` |
| SelfMX response time | <100ms | >500ms | Health endpoint timing |
| Error rate | 0-1% | >5% | `docker logs selfmx-app \| grep -i error` |
| Disk usage | <80% | >90% | `df -h /var/lib/docker` |

### Monitoring Commands

```bash
# Container resource usage
docker stats --no-stream selfmx-app selfmx-sqlserver selfmx-caddy

# SQL Server active connections
docker exec selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N \
  -Q "SELECT COUNT(*) as ActiveConnections FROM sys.dm_exec_sessions WHERE is_user_process = 1"

# Recent errors (last hour)
docker logs selfmx-app --since 1h 2>&1 | grep -i error | tail -10

# Disk usage
df -h /var/lib/docker

# Backup timer status
systemctl status selfmx-backup.timer
```

### Verification Schedule

| Time | Action | Expected Result |
|------|--------|-----------------|
| +5 min | All post-deploy queries | All pass |
| +1 hour | Check error logs | No new errors |
| +1 hour | Verify API functionality | All endpoints respond |
| +4 hours | Full verification pass | All metrics normal |
| +24 hours | Check first automated backup | Backup completed |

---

## CHECKLIST SUMMARY

### Pre-Deploy (Red - STOP if any fail)
- [ ] RAM >= 3GB available
- [ ] Disk >= 5GB available
- [ ] Docker installed and running
- [ ] Ports 80, 443 available
- [ ] DNS configured correctly
- [ ] SQL Server password meets complexity requirements
- [ ] **[Migration only]** Baseline counts recorded and saved
- [ ] **[Migration only]** SQLite databases backed up

### Deploy
- [ ] SQL Server container started
- [ ] SQL Server container healthy
- [ ] SelfMX container started
- [ ] SelfMX container healthy
- [ ] **[Migration only]** Migration endpoint executed successfully

### Post-Deploy (Within 5 Minutes)
- [ ] Health endpoint returns 200
- [ ] SQL verification queries pass
- [ ] All tables exist with correct schema
- [ ] Indexes and foreign keys created
- [ ] **[Migration only]** Row counts match baseline
- [ ] API authentication works
- [ ] Backup capability verified

### Monitoring (24 Hours)
- [ ] +1h check: No errors in logs
- [ ] +4h check: API functioning normally
- [ ] +24h check: First backup completed
- [ ] SQL Server memory usage stable
- [ ] Close deployment ticket

### Rollback Ready
- [ ] Previous SQLite config backed up
- [ ] SQLite backup available for restore
- [ ] Rollback procedures documented and understood

---

## Quick Reference

```bash
# View all logs
docker compose -f /opt/selfmx/docker-compose.yml logs -f

# Restart services
systemctl restart selfmx

# SQL Server shell
docker exec -it selfmx-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -N

# Check health
curl -sS https://$SELFMX_DOMAIN/health | jq .

# Migration status
curl -s http://localhost:5000/v1/migration/status \
  -H "Authorization: Bearer $ADMIN_API_KEY" | jq
```

---

*Generated by Deployment Verification Agent - 2026-01-26*
