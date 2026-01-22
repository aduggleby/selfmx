#!/bin/bash
# Test the install script on a fresh Hetzner Cloud server
# Requires: hcloud CLI, HCLOUD_TOKEN environment variable

set -euo pipefail

SERVER_NAME="claude-selfmx"
SERVER_TYPE="cx22"  # 2 vCPU, 4GB RAM
IMAGE="ubuntu-24.04"
LOCATION="nbg1"
SSH_KEY_NAME="${HCLOUD_SSH_KEY:-default}"

info() { echo "[INFO] $*"; }
error() { echo "[ERROR] $*" >&2; }
fatal() { error "$*"; exit 1; }

# Check for hcloud CLI
if ! command -v hcloud &>/dev/null; then
    fatal "hcloud CLI not found. Install with: brew install hcloud"
fi

# Check for HCLOUD_TOKEN
if [ -z "${HCLOUD_TOKEN:-}" ]; then
    fatal "HCLOUD_TOKEN environment variable not set"
fi

# Cleanup function
cleanup() {
    info "Cleaning up..."
    hcloud server delete "$SERVER_NAME" --force 2>/dev/null || true
}

# Delete existing server if it exists
if hcloud server describe "$SERVER_NAME" &>/dev/null; then
    info "Deleting existing server..."
    hcloud server delete "$SERVER_NAME" --force
    sleep 5
fi

# Create new server
info "Creating Hetzner server: $SERVER_NAME"
SERVER_JSON=$(hcloud server create \
    --name "$SERVER_NAME" \
    --type "$SERVER_TYPE" \
    --image "$IMAGE" \
    --location "$LOCATION" \
    --ssh-key "$SSH_KEY_NAME" \
    -o json)

SERVER_IP=$(echo "$SERVER_JSON" | jq -r '.server.public_net.ipv4.ip')
info "Server IP: $SERVER_IP"

# Wait for SSH to be available
info "Waiting for SSH to be available..."
for i in {1..30}; do
    if ssh -o ConnectTimeout=5 -o StrictHostKeyChecking=no root@"$SERVER_IP" echo "ready" 2>/dev/null; then
        info "SSH is ready!"
        break
    fi
    sleep 10
done

# Copy install script to server
info "Copying install script to server..."
scp -o StrictHostKeyChecking=no deploy/install.sh root@"$SERVER_IP":/tmp/install.sh

# Run the install script with test values
info "Running install script..."
ssh -o StrictHostKeyChecking=no root@"$SERVER_IP" bash <<'EOF'
set -e

# Set non-interactive mode with test values
export SELFMX_DOMAIN="test.selfmx.local"
export SELFMX_EMAIL="admin@test.selfmx.local"
export SELFMX_PASSWORD="testpassword123456"
export AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
export AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
export AWS_REGION="us-east-1"
export HTTP_PORT="80"
export HTTPS_PORT="443"
export BACKUP_TIME="03:00"

# Note: AWS validation will fail with example credentials
# For actual testing, use real credentials or mock the validation

bash /tmp/install.sh
EOF

info "Installation complete!"
info "Test server IP: $SERVER_IP"
info ""
info "To connect: ssh root@$SERVER_IP"
info "To delete:  hcloud server delete $SERVER_NAME"
