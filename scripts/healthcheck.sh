#!/bin/sh
# SelfMX Health Check Script
# Returns 0 if healthy, 1 if unhealthy. Uses the port from ASPNETCORE_URLS (default 17400) and a
# Host header accepted by AllowedHosts, so host filtering does not reject the probe.

PORT=$(echo "${ASPNETCORE_URLS:-http://+:17400}" | sed -E 's#^[^;]*:([0-9]+).*$#\1#')
HOST=$(echo "${AllowedHosts:-*}" | cut -d';' -f1)
case "$HOST" in
    ""|"*") HOST=localhost ;;
    "*."*) HOST="health${HOST#\*}" ;;
esac
wget -qO- --header "Host: ${HOST}" "http://127.0.0.1:${PORT:-17400}/ready" || exit 1
