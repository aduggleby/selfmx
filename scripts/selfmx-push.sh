#!/usr/bin/env bash
# =============================================================================
# selfmx-push.sh
#
# Run the full build and push pipeline using Ando.
# Uses --dind to enable Docker-in-Docker for building container images.
#
# Usage:
#   ./selfmx-push.sh
#
# Prerequisites:
# - GITHUB_TOKEN env var set (for ghcr.io push)
# - CLOUDFLARE_API_TOKEN env var set (for website deployment)
# - CLOUDFLARE_ACCOUNT_ID env var set (for website deployment)
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."

# Clean up Syncthing conflict files first
"$SCRIPT_DIR/clean.sh"

cd "$REPO_ROOT"

# Run ando with push profile and Docker-in-Docker enabled
ando run -p push --dind "$@"
