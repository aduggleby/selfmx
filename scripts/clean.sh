#!/bin/bash
# Clean up Syncthing conflict files
find "$(dirname "$0")/.." -name "*.sync-conflict-*" -type f -delete 2>/dev/null || true
