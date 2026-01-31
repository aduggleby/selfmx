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

# Export GITHUB_TOKEN from gh CLI if not already set
if [[ -z "${GITHUB_TOKEN:-}" ]]; then
    export GITHUB_TOKEN=$(gh auth token 2>/dev/null)
    if [[ -z "$GITHUB_TOKEN" ]]; then
        echo "Error: GITHUB_TOKEN not set and 'gh auth token' failed."
        echo "Please run 'gh auth login' or set GITHUB_TOKEN environment variable."
        exit 1
    fi
    echo "Using GitHub token from gh CLI"
fi

# Run ando with publish profile and Docker-in-Docker enabled
ando run -p publish --dind "$@"
