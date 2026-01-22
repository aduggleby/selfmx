#!/bin/bash
# SelfMX Install Script
# Usage: curl -fsSL https://get.selfmx.com/install.sh | bash
#
# Environment variables for non-interactive mode:
#   SELFMX_DOMAIN, SELFMX_PASSWORD, AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY
#   AWS_REGION, HTTP_PORT, HTTPS_PORT, SELFMX_EMAIL

set -euo pipefail

# ============================================
# Configuration
# ============================================
SELFMX_VERSION="${SELFMX_VERSION:-latest}"
INSTALL_DIR="/opt/selfmx"
DATA_DIR="/var/lib/selfmx"
BACKUP_DIR="/var/backups/selfmx"
LOG_FILE="/var/log/selfmx-install.log"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ============================================
# Logging Functions
# ============================================
log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"; }
info() { echo -e "${BLUE}[INFO]${NC} $*" | tee -a "$LOG_FILE"; }
success() { echo -e "${GREEN}[SUCCESS]${NC} $*" | tee -a "$LOG_FILE"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $*" | tee -a "$LOG_FILE"; }
error() { echo -e "${RED}[ERROR]${NC} $*" | tee -a "$LOG_FILE" >&2; }
fatal() { error "$*"; exit 1; }

# ============================================
# Main Function (prevents partial execution)
# ============================================
main() {
    # Create log file
    mkdir -p "$(dirname "$LOG_FILE")"
    touch "$LOG_FILE"

    log "=========================================="
    log "SelfMX Installation Started"
    log "=========================================="

    preflight_checks
    detect_existing_installation
    install_dependencies
    gather_configuration
    generate_admin_password_hash
    create_directories
    generate_config_files
    setup_backup_system
    setup_systemd_services
    pull_and_start
    verify_installation
    display_completion_message
}

# ============================================
# Pre-flight Checks
# ============================================
preflight_checks() {
    info "Running pre-flight checks..."

    # Check root privileges
    if [ "$(id -u)" -ne 0 ]; then
        fatal "This script must be run as root. Use: sudo bash or run as root."
    fi

    # Check OS
    if [ ! -f /etc/os-release ]; then
        fatal "Cannot detect operating system."
    fi

    source /etc/os-release

    if [ "$ID" != "ubuntu" ]; then
        fatal "This script requires Ubuntu. Detected: $ID"
    fi

    if [ "${VERSION_ID%%.*}" -lt 22 ]; then
        warn "Ubuntu 24.04 LTS is recommended. Detected: $VERSION_ID"
        read -r -p "Continue anyway? [y/N]: " continue_anyway
        if [[ ! "$continue_anyway" =~ ^[Yy] ]]; then
            fatal "Installation cancelled."
        fi
    fi

    # Check available disk space (require at least 5GB)
    available_space=$(df / --output=avail -B1G | tail -1 | tr -d ' ')
    if [ "$available_space" -lt 5 ]; then
        fatal "Insufficient disk space. Required: 5GB, Available: ${available_space}GB"
    fi

    # Check if ports are available
    for port in 80 443; do
        if ss -tlnp | grep -q ":${port} "; then
            process=$(ss -tlnp | grep ":${port} " | awk '{print $NF}')
            warn "Port $port is in use by: $process"
            read -r -p "Stop the conflicting service and continue? [y/N]: " stop_service
            if [[ ! "$stop_service" =~ ^[Yy] ]]; then
                fatal "Port $port is required for Caddy. Please free it and retry."
            fi
        fi
    done

    success "Pre-flight checks passed."
}

# ============================================
# Detect Existing Installation
# ============================================
detect_existing_installation() {
    if [ -f "${INSTALL_DIR}/.env" ]; then
        info "Existing SelfMX installation detected at ${INSTALL_DIR}"
        echo ""
        echo "Options:"
        echo "  [U] Update to latest version (preserve configuration)"
        echo "  [R] Reconfigure (edit settings)"
        echo "  [B] Backup and clean reinstall"
        echo "  [C] Cancel"
        echo ""
        read -r -p "Select option [U/R/B/C]: " option

        case "$option" in
            [Uu])
                info "Updating to latest version..."
                EXISTING_INSTALL="update"
                ;;
            [Rr])
                info "Reconfiguring..."
                EXISTING_INSTALL="reconfigure"
                ;;
            [Bb])
                info "Creating backup before reinstall..."
                create_manual_backup
                EXISTING_INSTALL="reinstall"
                ;;
            *)
                info "Installation cancelled."
                exit 0
                ;;
        esac
    else
        EXISTING_INSTALL="fresh"
    fi
}

# ============================================
# Install Dependencies
# ============================================
install_dependencies() {
    info "Installing dependencies..."

    export DEBIAN_FRONTEND=noninteractive

    # Update package list
    apt-get update -qq

    # Install prerequisites
    for pkg in apt-transport-https ca-certificates curl gnupg lsb-release sqlite3; do
        if ! dpkg -s "$pkg" &>/dev/null; then
            info "Installing $pkg..."
            apt-get install -y "$pkg"
        fi
    done

    # Install Docker if not present
    if ! command -v docker &>/dev/null; then
        info "Installing Docker..."

        # Add Docker's official GPG key
        install -m 0755 -d /etc/apt/keyrings
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg | \
            gpg --dearmor -o /etc/apt/keyrings/docker.gpg
        chmod a+r /etc/apt/keyrings/docker.gpg

        # Add Docker repository
        echo \
            "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
            https://download.docker.com/linux/ubuntu \
            $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
            tee /etc/apt/sources.list.d/docker.list > /dev/null

        # Install Docker
        apt-get update -qq
        apt-get install -y docker-ce docker-ce-cli containerd.io \
            docker-buildx-plugin docker-compose-plugin

        # Start and enable Docker
        systemctl enable --now docker

        success "Docker installed successfully."
    else
        info "Docker already installed."
    fi

    # Verify Docker is running
    if ! systemctl is-active --quiet docker; then
        systemctl start docker
    fi

    success "Dependencies installed."
}

# ============================================
# Gather Configuration
# ============================================
gather_configuration() {
    info "Gathering configuration..."

    # Skip if updating and not reconfiguring
    if [ "$EXISTING_INSTALL" = "update" ]; then
        source "${INSTALL_DIR}/.env"
        return
    fi

    # Load existing values if reconfiguring
    if [ "$EXISTING_INSTALL" = "reconfigure" ] && [ -f "${INSTALL_DIR}/.env" ]; then
        source "${INSTALL_DIR}/.env"
    fi

    echo ""
    echo "╔════════════════════════════════════════════════════════════╗"
    echo "║              SelfMX Configuration                          ║"
    echo "╚════════════════════════════════════════════════════════════╝"
    echo ""

    # Domain
    if [ -z "${SELFMX_DOMAIN:-}" ]; then
        read -r -p "Domain name (e.g., mail.example.com): " SELFMX_DOMAIN
        if [ -z "$SELFMX_DOMAIN" ]; then
            fatal "Domain is required."
        fi
    else
        echo "Domain: $SELFMX_DOMAIN"
    fi

    # Email for SSL certificates
    if [ -z "${SELFMX_EMAIL:-}" ]; then
        default_email="admin@${SELFMX_DOMAIN}"
        read -r -p "Email for SSL certificates [$default_email]: " SELFMX_EMAIL
        SELFMX_EMAIL="${SELFMX_EMAIL:-$default_email}"
    fi

    # Admin Password
    if [ -z "${SELFMX_PASSWORD:-}" ]; then
        while true; do
            read -r -s -p "Admin password (min 12 characters): " SELFMX_PASSWORD
            echo ""
            if [ ${#SELFMX_PASSWORD} -lt 12 ]; then
                warn "Password must be at least 12 characters."
                continue
            fi
            read -r -s -p "Confirm password: " password_confirm
            echo ""
            if [ "$SELFMX_PASSWORD" != "$password_confirm" ]; then
                warn "Passwords do not match."
                continue
            fi
            break
        done
    fi

    echo ""
    echo "─────────────────────────────────────────────────────────────"
    echo "                    AWS Configuration"
    echo "─────────────────────────────────────────────────────────────"

    # AWS Credentials
    if [ -z "${AWS_ACCESS_KEY_ID:-}" ]; then
        read -r -p "AWS Access Key ID: " AWS_ACCESS_KEY_ID
        if [ -z "$AWS_ACCESS_KEY_ID" ]; then
            fatal "AWS Access Key ID is required for SES integration."
        fi
    fi

    if [ -z "${AWS_SECRET_ACCESS_KEY:-}" ]; then
        read -r -s -p "AWS Secret Access Key: " AWS_SECRET_ACCESS_KEY
        echo ""
        if [ -z "$AWS_SECRET_ACCESS_KEY" ]; then
            fatal "AWS Secret Access Key is required for SES integration."
        fi
    fi

    if [ -z "${AWS_REGION:-}" ]; then
        read -r -p "AWS Region [us-east-1]: " AWS_REGION
        AWS_REGION="${AWS_REGION:-us-east-1}"
    fi

    # Validate AWS credentials (skip in test mode)
    if [ "${SKIP_AWS_VALIDATION:-}" = "1" ]; then
        warn "Skipping AWS credential validation (test mode)"
    else
        info "Validating AWS credentials..."
        if ! AWS_ACCESS_KEY_ID="$AWS_ACCESS_KEY_ID" \
             AWS_SECRET_ACCESS_KEY="$AWS_SECRET_ACCESS_KEY" \
             AWS_DEFAULT_REGION="$AWS_REGION" \
             docker run --rm -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY -e AWS_DEFAULT_REGION \
             amazon/aws-cli sts get-caller-identity &>/dev/null; then
            fatal "AWS credential validation failed. Please check your Access Key ID and Secret."
        fi
        success "AWS credentials validated."
    fi

    echo ""
    echo "─────────────────────────────────────────────────────────────"
    echo "              Cloudflare Configuration (Optional)"
    echo "─────────────────────────────────────────────────────────────"

    if [ -z "${CLOUDFLARE_API_TOKEN+x}" ]; then
        read -r -p "Cloudflare API Token (press Enter to skip): " CLOUDFLARE_API_TOKEN
    fi

    if [ -n "$CLOUDFLARE_API_TOKEN" ] && [ -z "${CLOUDFLARE_ZONE_ID:-}" ]; then
        read -r -p "Cloudflare Zone ID: " CLOUDFLARE_ZONE_ID
    fi

    echo ""
    echo "─────────────────────────────────────────────────────────────"
    echo "                  Port Configuration"
    echo "─────────────────────────────────────────────────────────────"

    if [ -z "${HTTP_PORT:-}" ]; then
        read -r -p "HTTP Port [80]: " HTTP_PORT
        HTTP_PORT="${HTTP_PORT:-80}"
    fi

    if [ -z "${HTTPS_PORT:-}" ]; then
        read -r -p "HTTPS Port [443]: " HTTPS_PORT
        HTTPS_PORT="${HTTPS_PORT:-443}"
    fi

    echo ""
    echo "─────────────────────────────────────────────────────────────"
    echo "                  Backup Configuration"
    echo "─────────────────────────────────────────────────────────────"

    if [ -z "${BACKUP_TIME:-}" ]; then
        read -r -p "Daily backup time (24h format) [03:00]: " BACKUP_TIME
        BACKUP_TIME="${BACKUP_TIME:-03:00}"
    fi

    # Confirm configuration
    echo ""
    echo "╔════════════════════════════════════════════════════════════╗"
    echo "║                  Configuration Summary                     ║"
    echo "╚════════════════════════════════════════════════════════════╝"
    echo ""
    echo "  Domain:           $SELFMX_DOMAIN"
    echo "  Email:            $SELFMX_EMAIL"
    echo "  AWS Region:       $AWS_REGION"
    echo "  Cloudflare:       ${CLOUDFLARE_API_TOKEN:+Configured}${CLOUDFLARE_API_TOKEN:-Not configured}"
    echo "  Ports:            HTTP=$HTTP_PORT, HTTPS=$HTTPS_PORT"
    echo "  Backup Time:      $BACKUP_TIME"
    echo ""

    if [ "${AUTO_CONFIRM:-}" = "1" ]; then
        info "Auto-confirming installation (non-interactive mode)"
    else
        read -r -p "Proceed with installation? [Y/n]: " proceed
        if [[ "$proceed" =~ ^[Nn] ]]; then
            fatal "Installation cancelled."
        fi
    fi

    success "Configuration gathered."
}

# ============================================
# Generate Admin Password Hash
# ============================================
generate_admin_password_hash() {
    info "Generating admin password hash (BCrypt)..."

    # Generate BCrypt hash of admin password using htpasswd
    # BCrypt is appropriate for admin passwords because they're user-chosen (low entropy)
    SELFMX_ADMIN_PASSWORD_HASH=$(docker run --rm \
        httpd:alpine htpasswd -nbBC 12 admin "$SELFMX_PASSWORD" | cut -d: -f2)

    success "Admin password hash generated."
}

# ============================================
# Create Directories
# ============================================
create_directories() {
    info "Creating directories..."

    mkdir -p "$INSTALL_DIR"
    mkdir -p "$DATA_DIR"
    mkdir -p "$BACKUP_DIR/daily"
    mkdir -p "$BACKUP_DIR/monthly"
    mkdir -p /var/log/selfmx

    # Set permissions
    chmod 750 "$INSTALL_DIR"
    chmod 750 "$DATA_DIR"
    chmod 750 "$BACKUP_DIR"

    success "Directories created."
}

# ============================================
# Generate Configuration Files
# ============================================
generate_config_files() {
    info "Generating configuration files..."

    # Generate .env file
    cat > "${INSTALL_DIR}/.env" <<EOF
# SelfMX Configuration
# Generated: $(date -Iseconds)

# Domain
SELFMX_DOMAIN=${SELFMX_DOMAIN}
SELFMX_EMAIL=${SELFMX_EMAIL}
SELFMX_VERSION=${SELFMX_VERSION}

# Admin Authentication (BCrypt hash for browser UI login)
# API keys are created via the admin UI after first login
SELFMX_ADMIN_PASSWORD_HASH=${SELFMX_ADMIN_PASSWORD_HASH}

# AWS Configuration
AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
AWS_REGION=${AWS_REGION}

# Cloudflare Configuration (optional)
CLOUDFLARE_API_TOKEN=${CLOUDFLARE_API_TOKEN:-}
CLOUDFLARE_ZONE_ID=${CLOUDFLARE_ZONE_ID:-}

# Ports
HTTP_PORT=${HTTP_PORT}
HTTPS_PORT=${HTTPS_PORT}

# Backup
BACKUP_TIME=${BACKUP_TIME}
EOF

    chmod 640 "${INSTALL_DIR}/.env"

    # Generate docker-compose.yml
    cat > "${INSTALL_DIR}/docker-compose.yml" <<'COMPOSE_EOF'
version: "3.9"

services:
  caddy:
    image: caddy:2-alpine
    container_name: selfmx-caddy
    restart: unless-stopped
    ports:
      - "${HTTP_PORT:-80}:80"
      - "${HTTPS_PORT:-443}:443"
      - "${HTTPS_PORT:-443}:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    networks:
      - selfmx-net
    depends_on:
      selfmx:
        condition: service_healthy
    environment:
      - SELFMX_DOMAIN=${SELFMX_DOMAIN}
      - SELFMX_EMAIL=${SELFMX_EMAIL}

  selfmx:
    image: ghcr.io/aduggleby/selfmx:${SELFMX_VERSION:-latest}
    container_name: selfmx-app
    restart: unless-stopped
    expose:
      - "5000"
    volumes:
      - selfmx_data:/app/data
    networks:
      - selfmx-net
    environment:
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/selfmx.db
      - ConnectionStrings__HangfireConnection=Data Source=/app/data/selfmx-hangfire.db
      - ConnectionStrings__AuditConnection=Data Source=/app/data/audit.db
      - App__AdminPasswordHash=${SELFMX_ADMIN_PASSWORD_HASH}
      - App__SessionExpirationDays=30
      - App__MaxLoginAttemptsPerMinute=5
      - App__MaxApiRequestsPerMinute=100
      - App__VerificationTimeout=72:00:00
      - App__VerificationPollInterval=00:05:00
      - Aws__Region=${AWS_REGION:-us-east-1}
      - Aws__AccessKeyId=${AWS_ACCESS_KEY_ID}
      - Aws__SecretAccessKey=${AWS_SECRET_ACCESS_KEY}
      - Cloudflare__ApiToken=${CLOUDFLARE_API_TOKEN:-}
      - Cloudflare__ZoneId=${CLOUDFLARE_ZONE_ID:-}
      - Cors__Origins__0=https://${SELFMX_DOMAIN}
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://127.0.0.1:5000/health"]
      interval: 30s
      timeout: 10s
      start_period: 15s
      retries: 3

networks:
  selfmx-net:
    driver: bridge

volumes:
  caddy_data:
  caddy_config:
  selfmx_data:
COMPOSE_EOF

    # Generate Caddyfile
    cat > "${INSTALL_DIR}/Caddyfile" <<CADDY_EOF
{
    email ${SELFMX_EMAIL}
}

${SELFMX_DOMAIN} {
    encode gzip zstd

    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Frame-Options "DENY"
        X-Content-Type-Options "nosniff"
        X-XSS-Protection "1; mode=block"
        Referrer-Policy "strict-origin-when-cross-origin"
        -Server
    }

    reverse_proxy selfmx:5000 {
        health_uri /health
        health_interval 30s
        health_timeout 10s
    }

    log {
        output file /data/access.log {
            roll_size 10mb
            roll_keep 5
        }
    }
}

www.${SELFMX_DOMAIN} {
    redir https://${SELFMX_DOMAIN}{uri} permanent
}
CADDY_EOF

    success "Configuration files generated."
}

# ============================================
# Setup Backup System
# ============================================
setup_backup_system() {
    info "Setting up backup system..."

    # Create backup script
    cat > /usr/local/bin/selfmx-backup <<'BACKUP_EOF'
#!/bin/bash
set -euo pipefail

BACKUP_DIR="/var/backups/selfmx"
INSTALL_DIR="/opt/selfmx"
DATA_VOLUME="selfmx_selfmx_data"
RETENTION_DAILY=7
RETENTION_MONTHLY=12

# Logging
log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
info() { log "[INFO] $*"; }
error() { log "[ERROR] $*" >&2; }
warn() { log "[WARN] $*"; }

# Determine backup type
TODAY=$(date +%Y-%m-%d)
DAY_OF_MONTH=$(date +%-d)

if [ "$DAY_OF_MONTH" -eq 1 ]; then
    BACKUP_TYPE="monthly"
else
    BACKUP_TYPE="daily"
fi

BACKUP_FILE="${BACKUP_DIR}/${BACKUP_TYPE}/selfmx-${TODAY}.tar.gz"

info "Starting ${BACKUP_TYPE} backup..."

# Create temp directory
TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

# Use SQLite .backup for atomic point-in-time snapshot
info "Creating atomic database backup..."
docker exec selfmx-app sh -c "
    sqlite3 /app/data/selfmx.db '.backup /tmp/selfmx.db.backup' && \
    sqlite3 /app/data/selfmx-hangfire.db '.backup /tmp/selfmx-hangfire.db.backup' && \
    sqlite3 /app/data/audit.db '.backup /tmp/audit.db.backup' 2>/dev/null || true && \
    mv /tmp/selfmx.db.backup /app/data/selfmx.db.backup && \
    mv /tmp/selfmx-hangfire.db.backup /app/data/selfmx-hangfire.db.backup && \
    mv /tmp/audit.db.backup /app/data/audit.db.backup 2>/dev/null || true
"

# Copy backup files from volume
info "Copying backup files from Docker volume..."
docker run --rm \
    -v "${DATA_VOLUME}:/data:ro" \
    -v "${TMP_DIR}:/backup" \
    alpine:latest \
    sh -c "cp /data/*.backup /backup/ 2>/dev/null; \
           for f in /backup/*.backup; do mv \"\$f\" \"\${f%.backup}\"; done"

# Clean up backup files in container
docker exec selfmx-app rm -f /app/data/*.backup 2>/dev/null || true

# Copy configuration files
cp "${INSTALL_DIR}/docker-compose.yml" "${TMP_DIR}/docker-compose.yml"
cp "${INSTALL_DIR}/Caddyfile" "${TMP_DIR}/Caddyfile"

# Encrypt .env if age is available
if command -v age &>/dev/null && [ -f "${INSTALL_DIR}/.backup-key" ]; then
    info "Encrypting configuration with age..."
    age -e -i "${INSTALL_DIR}/.backup-key" -o "${TMP_DIR}/.env.age" "${INSTALL_DIR}/.env"
else
    warn "age not installed or backup key missing - storing .env unencrypted"
    cp "${INSTALL_DIR}/.env" "${TMP_DIR}/.env"
fi

# Create metadata
cat > "${TMP_DIR}/metadata.json" <<EOF
{
    "backup_date": "$(date -Iseconds)",
    "backup_type": "${BACKUP_TYPE}",
    "selfmx_version": "$(docker inspect selfmx-app --format '{{.Config.Image}}' 2>/dev/null || echo 'unknown')",
    "hostname": "$(hostname)"
}
EOF

# Create tarball
info "Creating backup archive: ${BACKUP_FILE}"
tar -czf "${BACKUP_FILE}" -C "${TMP_DIR}" .

# Verify backup
if ! tar -tzf "${BACKUP_FILE}" > /dev/null 2>&1; then
    error "Backup verification failed!"
    exit 1
fi

BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
info "Backup complete: ${BACKUP_FILE} (${BACKUP_SIZE})"

# Rotation: Remove old backups
info "Rotating old backups..."
find "${BACKUP_DIR}/daily" -name "*.tar.gz" -mtime +${RETENTION_DAILY} -delete
find "${BACKUP_DIR}/monthly" -name "*.tar.gz" -mtime +$((RETENTION_MONTHLY * 30)) -delete

info "Backup rotation complete."
BACKUP_EOF

    chmod +x /usr/local/bin/selfmx-backup

    # Create restore script
    cat > /usr/local/bin/selfmx-restore <<'RESTORE_EOF'
#!/bin/bash
set -euo pipefail

BACKUP_DIR="/var/backups/selfmx"
INSTALL_DIR="/opt/selfmx"
DATA_VOLUME="selfmx_selfmx_data"

# Logging
info() { echo "[INFO] $*"; }
error() { echo "[ERROR] $*" >&2; }
fatal() { error "$*"; exit 1; }

# Usage
if [ $# -lt 1 ]; then
    echo "Usage: selfmx-restore <backup-file>"
    echo ""
    echo "Available backups:"
    echo "  Daily:"
    ls -1 "${BACKUP_DIR}/daily/"*.tar.gz 2>/dev/null || echo "    (none)"
    echo "  Monthly:"
    ls -1 "${BACKUP_DIR}/monthly/"*.tar.gz 2>/dev/null || echo "    (none)"
    exit 1
fi

BACKUP_FILE="$1"

if [ ! -f "$BACKUP_FILE" ]; then
    fatal "Backup file not found: $BACKUP_FILE"
fi

info "Restoring from: $BACKUP_FILE"

# Create temp directory
TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

# Extract backup
info "Extracting backup..."
tar -xzf "$BACKUP_FILE" -C "$TMP_DIR"

# Verify BEFORE stopping services
info "Verifying database integrity BEFORE stopping services..."
for db in selfmx.db selfmx-hangfire.db audit.db; do
    if [ -f "${TMP_DIR}/${db}" ]; then
        info "Checking ${db}..."
        if ! sqlite3 "${TMP_DIR}/${db}" "PRAGMA integrity_check;" | grep -q "ok"; then
            fatal "Database integrity check failed for ${db} - aborting restore, services NOT stopped"
        fi
    fi
done
info "Database integrity verified - proceeding with restore"

# Decrypt .env if encrypted
if [ -f "${TMP_DIR}/.env.age" ] && [ -f "${INSTALL_DIR}/.backup-key" ]; then
    info "Decrypting configuration..."
    age -d -i "${INSTALL_DIR}/.backup-key" -o "${TMP_DIR}/.env" "${TMP_DIR}/.env.age"
fi

# Stop services ONLY after verification passes
info "Stopping SelfMX services..."
cd "$INSTALL_DIR"
docker compose down || true

# Restore data to volume
info "Restoring data to Docker volume..."
docker run --rm \
    -v "${DATA_VOLUME}:/data" \
    -v "${TMP_DIR}:/backup:ro" \
    alpine:latest \
    sh -c "rm -rf /data/* && cp -a /backup/*.db /data/ 2>/dev/null || true"

# Optionally restore configuration
read -r -p "Restore configuration files? (This will overwrite current config) [y/N]: " restore_config
if [[ "$restore_config" =~ ^[Yy] ]]; then
    cp "${TMP_DIR}/.env" "${INSTALL_DIR}/.env"
    cp "${TMP_DIR}/docker-compose.yml" "${INSTALL_DIR}/docker-compose.yml"
    cp "${TMP_DIR}/Caddyfile" "${INSTALL_DIR}/Caddyfile"
    info "Configuration restored."
fi

# Start services
info "Starting SelfMX services..."
docker compose up -d

# Wait for health check
info "Waiting for services to be healthy..."
for i in {1..30}; do
    if docker exec selfmx-app wget -qO- http://127.0.0.1:5000/health &>/dev/null; then
        info "SelfMX is healthy!"
        break
    fi
    sleep 2
done

info "Restore complete!"
RESTORE_EOF

    chmod +x /usr/local/bin/selfmx-restore

    # Create systemd timer for backups
    cat > /etc/systemd/system/selfmx-backup.service <<EOF
[Unit]
Description=SelfMX Backup
After=docker.service

[Service]
Type=oneshot
ExecStart=/usr/local/bin/selfmx-backup
StandardOutput=append:/var/log/selfmx/backup.log
StandardError=append:/var/log/selfmx/backup.log
EOF

    # Parse backup time
    BACKUP_HOUR="${BACKUP_TIME%%:*}"
    BACKUP_MINUTE="${BACKUP_TIME##*:}"

    cat > /etc/systemd/system/selfmx-backup.timer <<EOF
[Unit]
Description=Daily SelfMX Backup Timer

[Timer]
OnCalendar=*-*-* ${BACKUP_HOUR}:${BACKUP_MINUTE}:00
Persistent=true

[Install]
WantedBy=timers.target
EOF

    systemctl daemon-reload
    systemctl enable selfmx-backup.timer
    systemctl start selfmx-backup.timer

    success "Backup system configured."
}

# ============================================
# Setup Systemd Services
# ============================================
setup_systemd_services() {
    info "Setting up systemd services..."

    cat > /etc/systemd/system/selfmx.service <<EOF
[Unit]
Description=SelfMX Docker Compose Service
Documentation=https://github.com/aduggleby/selfmx
After=docker.service network-online.target
Requires=docker.service
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=${INSTALL_DIR}
ExecStartPre=/usr/bin/docker compose pull
ExecStart=/usr/bin/docker compose up
ExecStop=/usr/bin/docker compose down
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable selfmx.service

    success "Systemd services configured."
}

# ============================================
# Pull and Start Services
# ============================================
pull_and_start() {
    info "Pulling Docker images and starting services..."

    cd "$INSTALL_DIR"

    # Pull images
    docker compose pull

    # Start services
    systemctl start selfmx.service

    success "Services started."
}

# ============================================
# Verify Installation
# ============================================
verify_installation() {
    info "Verifying installation..."

    # Wait for services to be healthy
    local retries=30
    local i=0

    while [ $i -lt $retries ]; do
        if docker exec selfmx-app wget -qO- http://127.0.0.1:5000/health &>/dev/null; then
            success "SelfMX is healthy!"
            return 0
        fi
        i=$((i + 1))
        sleep 2
    done

    warn "Health check timed out. Services may still be starting."
    warn "Check logs with: docker compose -f ${INSTALL_DIR}/docker-compose.yml logs"
}

# ============================================
# Create Manual Backup
# ============================================
create_manual_backup() {
    info "Creating backup..."
    /usr/local/bin/selfmx-backup || warn "Backup failed, continuing with reinstall..."
}

# ============================================
# Display Completion Message
# ============================================
display_completion_message() {
    echo ""
    echo "╔════════════════════════════════════════════════════════════╗"
    echo "║          SelfMX Installation Complete!                     ║"
    echo "╚════════════════════════════════════════════════════════════╝"
    echo ""
    echo "  Access your SelfMX instance at:"
    echo "    https://${SELFMX_DOMAIN}"
    echo ""
    echo "  ─────────────────────────────────────────────────────────"
    echo "  AUTHENTICATION:"
    echo ""
    echo "  1. Log in with your admin password at:"
    echo "       https://${SELFMX_DOMAIN}/admin/login"
    echo ""
    echo "  2. Create API keys via the admin UI:"
    echo "       - Name your keys (e.g., 'Production', 'Staging')"
    echo "       - Scope keys to specific domains"
    echo "       - Keys use Resend format: re_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
    echo ""
    echo "  3. Use API keys in your application:"
    echo "       Authorization: Bearer re_your_api_key_here"
    echo "  ─────────────────────────────────────────────────────────"
    echo ""
    echo "  IMPORTANT: Before accessing SelfMX, ensure DNS is configured:"
    echo ""
    echo "    Add an A record pointing ${SELFMX_DOMAIN} to this server's IP"
    echo ""
    echo "    Verify with: dig ${SELFMX_DOMAIN} @8.8.8.8"
    echo "  ─────────────────────────────────────────────────────────"
    echo ""
    echo "  Useful commands:"
    echo "    View logs:        docker compose -f ${INSTALL_DIR}/docker-compose.yml logs -f"
    echo "    Restart:          systemctl restart selfmx"
    echo "    Manual backup:    selfmx-backup"
    echo "    Restore backup:   selfmx-restore /var/backups/selfmx/daily/selfmx-YYYY-MM-DD.tar.gz"
    echo ""
    echo "  Backup location: ${BACKUP_DIR}"
    echo "    - Daily backups:   ${BACKUP_DIR}/daily/   (7 retained)"
    echo "    - Monthly backups: ${BACKUP_DIR}/monthly/ (12 retained)"
    echo "    - Includes: selfmx.db, selfmx-hangfire.db, audit.db, .env"
    echo ""
    echo "  ─────────────────────────────────────────────────────────"
    echo "  To backup to another location (S3, rsync, etc.):"
    echo ""
    echo "    # Using rclone to sync to S3:"
    echo "    rclone sync ${BACKUP_DIR} s3:mybucket/selfmx-backups"
    echo ""
    echo "    # Using rsync to another server:"
    echo "    rsync -avz ${BACKUP_DIR}/ user@backup-server:/backups/selfmx/"
    echo ""
    echo "    # Install rclone: curl https://rclone.org/install.sh | bash"
    echo "  ─────────────────────────────────────────────────────────"
    echo ""
    echo "  Installation log: ${LOG_FILE}"
    echo ""

    log "Installation completed successfully."
}

# ============================================
# Run Main Function
# ============================================
main "$@"
