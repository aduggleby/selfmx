#!/bin/sh
# SelfMX Health Check Script
# Returns 0 if healthy, 1 if unhealthy

wget -qO- http://127.0.0.1:5000/health || exit 1
