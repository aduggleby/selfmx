# Deployment Verification Checklist: SelfMX Docker Deployment

**Plan Reference:** `/home/alex/Source/selfmx/plans/feat-docker-deployment-hetzner-install-script.md`
**Generated:** 2026-01-22
**Deployment Type:** Docker + Caddy + SQLite (Hetzner Cloud)

---

## Data Invariants

These conditions must remain true throughout and after deployment:

```
[ ] Domain records preserve all fields: Id, Name, Status, CreatedAt, DnsRecordsJson
[ ] Domain Status values are valid: Pending, Verifying, Verified, Failed
[ ] All Domain.Name values remain unique (unique index constraint)
[ ] SQLite databases are not corrupted (integrity_check passes)
[ ] WAL mode is enabled on both databases
[ ] Backup files contain valid, restorable data
```

---

## Phase 1: Pre-Deployment Checks (Required)

### 1.1 System Requirements Verification

```bash
# Check Ubuntu version (require 22.04+, recommend 24.04)
cat /etc/os-release | grep VERSION_ID
# Expected: VERSION_ID="24.04" or higher

# Check available disk space (require >= 5GB)
df -h / | awk 'NR==2 {print $4}'
# Expected: >= 5G

# Check available memory (require >= 1GB, recommend >= 2GB)
free -h | awk '/Mem:/ {print $2}'
# Expected: >= 1.0G

# Check CPU cores
nproc
# Expected: >= 1 (recommend >= 2)
```

### 1.2 Network and Port Verification

```bash
# Check if port 80 is available
ss -tlnp | grep ':80 ' || echo "Port 80 available"
# Expected: "Port 80 available" (no output from ss)

# Check if port 443 is available
ss -tlnp | grep ':443 ' || echo "Port 443 available"
# Expected: "Port 443 available" (no output from ss)

# Check if port 5000 is available (internal API)
ss -tlnp | grep ':5000 ' || echo "Port 5000 available"
# Expected: "Port 5000 available"

# Check outbound connectivity to Docker Hub
curl -sI https://registry-1.docker.io/v2/ | head -1
# Expected: HTTP/2 401 (authentication required is fine, means connectivity works)

# Check outbound connectivity to GitHub Container Registry
curl -sI https://ghcr.io/v2/ | head -1
# Expected: HTTP/2 401
```

### 1.3 DNS Configuration Verification

```bash
# Replace SELFMX_DOMAIN with your actual domain
SELFMX_DOMAIN="mail.example.com"

# Check A record resolves to this server's IP
SERVER_IP=$(curl -4 -s ifconfig.me)
DNS_IP=$(dig +short $SELFMX_DOMAIN @8.8.8.8 | head -1)
echo "Server IP: $SERVER_IP"
echo "DNS resolves to: $DNS_IP"
[ "$SERVER_IP" = "$DNS_IP" ] && echo "DNS OK" || echo "DNS MISMATCH - STOP DEPLOYMENT"
# Expected: "DNS OK"

# Check DNS propagation from multiple resolvers
for resolver in 8.8.8.8 1.1.1.1 9.9.9.9; do
    echo "Resolver $resolver: $(dig +short $SELFMX_DOMAIN @$resolver | head -1)"
done
# Expected: All should return the same IP as SERVER_IP

# Verify no CAA records blocking Let's Encrypt
dig +short CAA $SELFMX_DOMAIN
# Expected: Empty OR contains "letsencrypt.org"
```

### 1.4 AWS Credentials Verification

```bash
# Set credentials (or use environment variables)
export AWS_ACCESS_KEY_ID="your-key"
export AWS_SECRET_ACCESS_KEY="your-secret"
export AWS_REGION="us-east-1"

# Verify credentials are valid
aws sts get-caller-identity
# Expected: Returns Account, UserId, Arn

# Verify SES permissions
aws sesv2 get-account
# Expected: Returns account details without error

# Check SES sending quota
aws sesv2 get-account --query 'SendQuota'
# Expected: Shows Max24HourSend, MaxSendRate, SentLast24Hours
```

### 1.5 Docker Prerequisites (if Docker not installed)

```bash
# Check if Docker is already installed
docker --version && docker compose version
# Expected: Docker version 24.x or higher, Compose v2.x

# If Docker not installed, verify can install
apt-get update && apt-cache show docker-ce | grep Version | head -1
# Expected: Shows available Docker CE version
```

---

## Phase 2: Pre-Deployment Baseline (Save These Values)

### 2.1 Existing Database State (if upgrading)

```bash
# Only run if /opt/selfmx exists (upgrade scenario)
if [ -d /opt/selfmx ]; then
    echo "=== EXISTING INSTALLATION DETECTED - SAVE THESE VALUES ==="

    # Get container data volume path
    VOLUME_PATH=$(docker volume inspect selfmx_selfmx_data --format '{{.Mountpoint}}')

    # Count domains by status
    echo "Domain counts by status:"
    docker exec selfmx-app sqlite3 /app/data/selfmx.db \
        "SELECT Status, COUNT(*) FROM Domains GROUP BY Status;"

    # Total domain count
    echo "Total domains:"
    docker exec selfmx-app sqlite3 /app/data/selfmx.db \
        "SELECT COUNT(*) FROM Domains;"

    # Get sample of domain names (for post-deploy verification)
    echo "Sample domains (first 5):"
    docker exec selfmx-app sqlite3 /app/data/selfmx.db \
        "SELECT Id, Name, Status FROM Domains LIMIT 5;"

    # Database file sizes
    echo "Database sizes:"
    docker exec selfmx-app ls -lh /app/data/*.db 2>/dev/null

    # Save baseline to file
    docker exec selfmx-app sqlite3 /app/data/selfmx.db \
        "SELECT Status, COUNT(*) FROM Domains GROUP BY Status;" \
        > /tmp/selfmx-baseline-$(date +%Y%m%d-%H%M%S).txt
    echo "Baseline saved to /tmp/selfmx-baseline-*.txt"
fi
```

### 2.2 Current Version Info (if upgrading)

```bash
# Current image version
docker inspect selfmx-app --format '{{.Config.Image}}' 2>/dev/null || echo "No existing installation"

# Current container uptime
docker inspect selfmx-app --format '{{.State.StartedAt}}' 2>/dev/null || echo "N/A"

# Current config backup
if [ -f /opt/selfmx/.env ]; then
    cp /opt/selfmx/.env /opt/selfmx/.env.backup-$(date +%Y%m%d-%H%M%S)
    echo "Config backed up"
fi
```

---

## Phase 3: Deployment Steps

### 3.1 Installation Execution

| Step | Command | Est. Time | Rollback |
|------|---------|-----------|----------|
| 1. Run install script | `curl -fsSL https://get.selfmx.com/install.sh \| bash` | 3-5 min | `systemctl stop selfmx && docker compose down` |
| 2. Verify Docker running | `systemctl is-active docker` | Instant | `systemctl start docker` |
| 3. Verify images pulled | `docker images \| grep selfmx` | Instant | `docker compose pull` |
| 4. Verify containers started | `docker ps -a \| grep selfmx` | Instant | `systemctl restart selfmx` |
| 5. Wait for health check | `docker exec selfmx-app wget -qO- http://127.0.0.1:5000/health` | 30-60s | Check logs |

### 3.2 Detailed Verification Commands

```bash
# Step 1: Verify install completed
[ -f /opt/selfmx/.env ] && echo "Config exists" || echo "MISSING CONFIG"
[ -f /opt/selfmx/docker-compose.yml ] && echo "Compose exists" || echo "MISSING COMPOSE"
[ -f /opt/selfmx/Caddyfile ] && echo "Caddyfile exists" || echo "MISSING CADDYFILE"

# Step 2: Check directory permissions
stat -c "%a %U:%G" /opt/selfmx
# Expected: 750 root:root

stat -c "%a %U:%G" /opt/selfmx/.env
# Expected: 640 root:root

# Step 3: Verify systemd service
systemctl status selfmx.service --no-pager
# Expected: Active: active (running)

systemctl is-enabled selfmx.service
# Expected: enabled

# Step 4: Verify backup timer
systemctl status selfmx-backup.timer --no-pager
# Expected: Active: active (waiting)

systemctl list-timers selfmx-backup.timer
# Expected: Shows next scheduled run
```

---

## Phase 4: Post-Deployment Verification (Within 5 Minutes)

### 4.1 Service Health Checks

```bash
# Check all containers are running and healthy
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
# Expected: selfmx-app and selfmx-caddy both "Up" with (healthy)

# Internal health check
docker exec selfmx-app wget -qO- http://127.0.0.1:5000/health
# Expected: {"status":"Healthy"...}

# Health check via Caddy (internal)
docker exec selfmx-caddy wget -qO- http://selfmx:5000/health
# Expected: {"status":"Healthy"...}

# External health check (replace domain)
curl -sS https://$SELFMX_DOMAIN/health
# Expected: {"status":"Healthy"...}
```

### 4.2 SSL Certificate Validation

```bash
# Check certificate issuer
echo | openssl s_client -connect $SELFMX_DOMAIN:443 -servername $SELFMX_DOMAIN 2>/dev/null | \
    openssl x509 -noout -issuer
# Expected: Contains "Let's Encrypt" or "ZeroSSL"

# Check certificate expiry
echo | openssl s_client -connect $SELFMX_DOMAIN:443 -servername $SELFMX_DOMAIN 2>/dev/null | \
    openssl x509 -noout -dates
# Expected: notAfter should be ~90 days from now

# Check certificate chain
echo | openssl s_client -connect $SELFMX_DOMAIN:443 -servername $SELFMX_DOMAIN 2>/dev/null | \
    openssl x509 -noout -text | grep -A1 "Subject:"
# Expected: CN = your domain

# Verify HTTPS redirect
curl -sI http://$SELFMX_DOMAIN | head -2
# Expected: HTTP/1.1 308 Permanent Redirect

# Check security headers
curl -sI https://$SELFMX_DOMAIN | grep -E "Strict-Transport|X-Frame|X-Content-Type"
# Expected: All three headers present
```

### 4.3 Database Connectivity and Integrity

```bash
# Check SQLite database exists
docker exec selfmx-app ls -la /app/data/
# Expected: selfmx.db and selfmx-hangfire.db present

# Verify WAL mode is enabled
docker exec selfmx-app sqlite3 /app/data/selfmx.db "PRAGMA journal_mode;"
# Expected: wal

# Database integrity check
docker exec selfmx-app sqlite3 /app/data/selfmx.db "PRAGMA integrity_check;"
# Expected: ok

docker exec selfmx-app sqlite3 /app/data/selfmx-hangfire.db "PRAGMA integrity_check;"
# Expected: ok

# Verify schema exists
docker exec selfmx-app sqlite3 /app/data/selfmx.db ".tables"
# Expected: Domains (and potentially __EFMigrationsHistory)

# Check Domain table structure
docker exec selfmx-app sqlite3 /app/data/selfmx.db ".schema Domains"
# Expected: CREATE TABLE with Id, Name, Status, CreatedAt, etc.
```

### 4.4 API Endpoint Testing

```bash
# Get API key from .env
API_KEY=$(grep SELFMX_API_KEY /opt/selfmx/.env | cut -d'=' -f2 | head -1)

# Test root endpoint (no auth required)
curl -sS https://$SELFMX_DOMAIN/
# Expected: {"status":"ok","timestamp":"..."}

# Test health endpoint (no auth required)
curl -sS https://$SELFMX_DOMAIN/health
# Expected: {"status":"Healthy",...}

# Test authenticated endpoint - list domains
curl -sS https://$SELFMX_DOMAIN/v1/domains \
    -H "Authorization: Bearer $API_KEY"
# Expected: {"data":[],...} or list of domains

# Test POST endpoint (create domain) - DRY RUN
curl -sS https://$SELFMX_DOMAIN/v1/domains \
    -H "Authorization: Bearer $API_KEY" \
    -H "Content-Type: application/json" \
    -d '{"name":"test-verify.example.com"}' \
    --write-out "\n%{http_code}"
# Expected: 201 Created (then clean up the test domain)

# Test unauthorized access (should fail)
curl -sS https://$SELFMX_DOMAIN/v1/domains \
    --write-out "\n%{http_code}"
# Expected: 401 Unauthorized
```

### 4.5 Compare with Pre-Deployment Baseline (if upgrading)

```bash
# Count domains by status (compare with Phase 2 baseline)
docker exec selfmx-app sqlite3 /app/data/selfmx.db \
    "SELECT Status, COUNT(*) FROM Domains GROUP BY Status;"
# Expected: Same counts as baseline

# Verify sample domains still exist
docker exec selfmx-app sqlite3 /app/data/selfmx.db \
    "SELECT Id, Name, Status FROM Domains LIMIT 5;"
# Expected: Same records as baseline

# Check for any NULL values in required fields
docker exec selfmx-app sqlite3 /app/data/selfmx.db \
    "SELECT COUNT(*) FROM Domains WHERE Name IS NULL OR Id IS NULL;"
# Expected: 0
```

---

## Phase 5: Backup Verification

### 5.1 Backup Creation Test

```bash
# Trigger manual backup
/usr/local/bin/selfmx-backup
# Expected: Completes without errors

# Verify backup was created
ls -la /var/backups/selfmx/daily/
# Expected: selfmx-YYYY-MM-DD.tar.gz file exists

# Check backup size (should be > 0)
LATEST_BACKUP=$(ls -t /var/backups/selfmx/daily/*.tar.gz | head -1)
du -h "$LATEST_BACKUP"
# Expected: Size > 10KB (depends on data)
```

### 5.2 Backup Integrity Check

```bash
# Verify tarball is valid
tar -tzf "$LATEST_BACKUP" > /dev/null && echo "Tarball OK" || echo "TARBALL CORRUPT"
# Expected: "Tarball OK"

# List backup contents
tar -tzf "$LATEST_BACKUP"
# Expected: Contains .env, docker-compose.yml, Caddyfile, selfmx.db, metadata.json

# Extract and verify database integrity
TMP_RESTORE=$(mktemp -d)
tar -xzf "$LATEST_BACKUP" -C "$TMP_RESTORE"
sqlite3 "$TMP_RESTORE/selfmx.db" "PRAGMA integrity_check;"
# Expected: ok

# Check metadata
cat "$TMP_RESTORE/metadata.json"
# Expected: Valid JSON with backup_date, selfmx_version

# Cleanup
rm -rf "$TMP_RESTORE"
```

### 5.3 Restore Dry-Run

```bash
# Create a test restore directory
TEST_RESTORE_DIR=$(mktemp -d)
tar -xzf "$LATEST_BACKUP" -C "$TEST_RESTORE_DIR"

# Verify all required files present
for file in selfmx.db .env docker-compose.yml Caddyfile metadata.json; do
    [ -f "$TEST_RESTORE_DIR/$file" ] && echo "$file: OK" || echo "$file: MISSING"
done
# Expected: All files OK

# Verify database can be opened and queried
sqlite3 "$TEST_RESTORE_DIR/selfmx.db" "SELECT COUNT(*) FROM Domains;"
# Expected: Number (matches production count)

# Verify configuration is readable
grep SELFMX_DOMAIN "$TEST_RESTORE_DIR/.env"
# Expected: Shows domain configuration

# Cleanup
rm -rf "$TEST_RESTORE_DIR"
echo "Dry-run restore completed successfully"
```

---

## Phase 6: Rollback Procedures

### 6.1 Rollback Decision Matrix

| Issue | Severity | Rollback Action |
|-------|----------|-----------------|
| Health check failing | Critical | Rollback to previous image |
| SSL not working | High | Check Caddy logs, may self-resolve |
| Database corruption | Critical | Restore from backup |
| API errors | High | Check logs, may need code rollback |
| Performance degradation | Medium | Investigate before rollback |

### 6.2 Rollback to Previous Version

```bash
# Step 1: Stop current services
cd /opt/selfmx
systemctl stop selfmx.service

# Step 2: Update .env to pin previous version
# (Find previous version in deployment notes or Docker history)
PREVIOUS_VERSION="sha-abc1234"  # Replace with actual version
sed -i "s/SELFMX_VERSION=.*/SELFMX_VERSION=$PREVIOUS_VERSION/" .env

# Step 3: Pull previous image
docker compose pull

# Step 4: Start with previous version
systemctl start selfmx.service

# Step 5: Verify rollback
docker inspect selfmx-app --format '{{.Config.Image}}'
# Expected: Contains $PREVIOUS_VERSION

# Step 6: Verify health
docker exec selfmx-app wget -qO- http://127.0.0.1:5000/health
# Expected: {"status":"Healthy"...}
```

### 6.3 Restore from Backup

```bash
# Step 1: List available backups
ls -la /var/backups/selfmx/daily/
ls -la /var/backups/selfmx/monthly/

# Step 2: Choose backup to restore
RESTORE_FILE="/var/backups/selfmx/daily/selfmx-YYYY-MM-DD.tar.gz"

# Step 3: Run restore script
/usr/local/bin/selfmx-restore "$RESTORE_FILE"
# Follow prompts

# Step 4: Verify restore
docker exec selfmx-app sqlite3 /app/data/selfmx.db "PRAGMA integrity_check;"
docker exec selfmx-app sqlite3 /app/data/selfmx.db "SELECT COUNT(*) FROM Domains;"

# Step 5: Test API
curl -sS https://$SELFMX_DOMAIN/health
```

### 6.4 Emergency Procedures

```bash
# EMERGENCY: Complete service failure
# =====================================

# 1. Check what's running
docker ps -a
systemctl status selfmx docker

# 2. Check disk space
df -h /

# 3. Check Docker logs
docker logs selfmx-app --tail 100
docker logs selfmx-caddy --tail 100

# 4. Check system logs
journalctl -u selfmx.service --since "1 hour ago" --no-pager

# 5. Force restart everything
systemctl restart docker
sleep 10
systemctl restart selfmx.service
sleep 30

# 6. If still failing, manual container start
cd /opt/selfmx
docker compose down
docker compose up -d
docker compose logs -f

# 7. Last resort: restore from backup
/usr/local/bin/selfmx-restore /var/backups/selfmx/daily/selfmx-YYYY-MM-DD.tar.gz
```

---

## Phase 7: Monitoring Setup

### 7.1 What to Monitor

| Metric | Check Command | Alert Condition |
|--------|---------------|-----------------|
| Health endpoint | `curl https://$DOMAIN/health` | Non-200 for > 1 min |
| Container status | `docker ps --filter name=selfmx` | Any container not "running" |
| SSL expiry | `openssl s_client` | < 14 days to expiry |
| Disk usage | `df -h /` | > 80% used |
| Database size | `ls -la /app/data/selfmx.db` | > 1GB (investigate) |
| Backup success | `/var/log/selfmx/backup.log` | Last backup > 25 hours ago |

### 7.2 Monitoring Commands (cron/systemd)

```bash
# Create monitoring script
cat > /usr/local/bin/selfmx-monitor << 'EOF'
#!/bin/bash
DOMAIN="${SELFMX_DOMAIN:-localhost}"
LOG_FILE="/var/log/selfmx/monitor.log"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" >> "$LOG_FILE"; }

# Health check
if ! curl -sf "https://$DOMAIN/health" > /dev/null 2>&1; then
    log "ALERT: Health check failed"
    # Add alerting here (email, Slack, etc.)
fi

# Container check
for container in selfmx-app selfmx-caddy; do
    if ! docker ps --format '{{.Names}}' | grep -q "^${container}$"; then
        log "ALERT: Container $container not running"
    fi
done

# Disk check
DISK_USAGE=$(df / --output=pcent | tail -1 | tr -d ' %')
if [ "$DISK_USAGE" -gt 80 ]; then
    log "ALERT: Disk usage at ${DISK_USAGE}%"
fi

# Backup check (should be less than 25 hours old)
LATEST_BACKUP=$(ls -t /var/backups/selfmx/daily/*.tar.gz 2>/dev/null | head -1)
if [ -n "$LATEST_BACKUP" ]; then
    BACKUP_AGE=$(( ($(date +%s) - $(stat -c %Y "$LATEST_BACKUP")) / 3600 ))
    if [ "$BACKUP_AGE" -gt 25 ]; then
        log "ALERT: Latest backup is ${BACKUP_AGE} hours old"
    fi
fi
EOF
chmod +x /usr/local/bin/selfmx-monitor

# Add to cron (every 5 minutes)
echo "*/5 * * * * /usr/local/bin/selfmx-monitor" | crontab -
```

### 7.3 Log Locations

| Log | Location | View Command |
|-----|----------|--------------|
| Install log | `/var/log/selfmx-install.log` | `tail -f /var/log/selfmx-install.log` |
| Backup log | `/var/log/selfmx/backup.log` | `tail -f /var/log/selfmx/backup.log` |
| App logs | Docker | `docker logs selfmx-app -f` |
| Caddy logs | Docker | `docker logs selfmx-caddy -f` |
| Caddy access | Docker volume | `docker exec selfmx-caddy cat /data/access.log` |
| Systemd service | journald | `journalctl -u selfmx.service -f` |

### 7.4 Spot Check Commands (Run at +1h, +4h, +24h)

```bash
# Quick health summary
echo "=== SelfMX Health Check $(date) ==="

echo -e "\n[Containers]"
docker ps --format "table {{.Names}}\t{{.Status}}" --filter name=selfmx

echo -e "\n[Health Endpoint]"
curl -sS https://$SELFMX_DOMAIN/health | jq .

echo -e "\n[Database Stats]"
docker exec selfmx-app sqlite3 /app/data/selfmx.db \
    "SELECT 'Domains: ' || COUNT(*) FROM Domains;"

echo -e "\n[Disk Usage]"
df -h / | tail -1

echo -e "\n[Recent Errors]"
docker logs selfmx-app --since 1h 2>&1 | grep -i error | tail -5 || echo "No recent errors"

echo -e "\n[Latest Backup]"
ls -lh /var/backups/selfmx/daily/ | tail -1
```

---

## Go/No-Go Checklist Summary

### Pre-Deploy (Required) - STOP if any fail

- [ ] Ubuntu version >= 22.04 verified
- [ ] Disk space >= 5GB available
- [ ] Ports 80, 443 available
- [ ] DNS A record points to server IP (verified with multiple resolvers)
- [ ] AWS credentials validated (sts:GetCallerIdentity succeeds)
- [ ] AWS SES permissions verified
- [ ] If upgrading: baseline database counts saved

### Deploy Steps

1. [ ] Run install script: `curl -fsSL https://get.selfmx.com/install.sh | bash`
2. [ ] Verify config files created in `/opt/selfmx/`
3. [ ] Verify systemd service enabled and running
4. [ ] Verify backup timer active

### Post-Deploy (Within 5 Minutes) - ROLLBACK if critical fails

- [ ] Health endpoint returns 200: `curl https://$DOMAIN/health`
- [ ] SSL certificate valid (Let's Encrypt issued)
- [ ] Security headers present (HSTS, X-Frame-Options)
- [ ] Database integrity check passes
- [ ] API authentication works (401 without key, 200 with key)
- [ ] If upgrading: domain counts match baseline

### Backup Verification (Within 30 Minutes)

- [ ] Manual backup creates valid tarball
- [ ] Backup contains all required files
- [ ] Extracted database passes integrity check
- [ ] Dry-run restore succeeds

### Monitoring (24 Hours)

- [ ] Monitoring script installed and running
- [ ] +1h spot check completed
- [ ] +4h spot check completed
- [ ] +24h spot check completed
- [ ] First automated backup succeeded

### Rollback Available

- [ ] Previous version identified for rollback
- [ ] Backup file confirmed for data restore
- [ ] Rollback procedure documented and tested

---

## Quick Reference Commands

```bash
# View logs
docker compose -f /opt/selfmx/docker-compose.yml logs -f

# Restart services
systemctl restart selfmx

# Manual backup
selfmx-backup

# Restore from backup
selfmx-restore /var/backups/selfmx/daily/selfmx-YYYY-MM-DD.tar.gz

# Check health
curl -sS https://$SELFMX_DOMAIN/health | jq .

# Database query
docker exec selfmx-app sqlite3 /app/data/selfmx.db "SELECT * FROM Domains;"

# Force pull latest image
cd /opt/selfmx && docker compose pull && systemctl restart selfmx
```

---

*Deployment Verification Checklist generated by Claude Opus 4.5 on 2026-01-22*
