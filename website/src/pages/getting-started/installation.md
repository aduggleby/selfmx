---
layout: ../../layouts/DocsLayout.astro
title: Installation
description: Deploy SelfMX on your Ubuntu server with a single command.
---

SelfMX can be installed on any Ubuntu 22.04+ server with a single command. The installer sets up Docker, Caddy (for automatic HTTPS), systemd services, and automated backups.

## Quick Install

Run this command from your local machine to install on a remote server:

```bash
ssh user@server 'curl -fsSL https://selfmx.com/install.sh | bash -s'
```

Or run directly on the server as root:

```bash
curl -fsSL https://selfmx.com/install.sh | bash -s
```

The interactive installer will prompt you for:

- **Domain name** - Where SelfMX will be hosted (e.g., `mail.example.com`)
- **Admin password** - For logging into the admin UI
- **AWS credentials** - Access Key ID, Secret, and Region for SES
- **Cloudflare credentials** (optional) - For automatic DNS record management

## Requirements

- **Ubuntu 22.04 LTS** or newer (tested on Hetzner with Ubuntu 24.04)
- **Root access** - The script must run as root
- **5GB disk space** minimum
- **Ports 80 and 443** available for Caddy
- **AWS account** with SES access configured

## What Gets Installed

The installer sets up:

- **Docker** - Container runtime
- **Caddy** - Reverse proxy with automatic HTTPS via Let's Encrypt
- **SelfMX** - The application container from `ghcr.io/aduggleby/selfmx`
- **Systemd service** - Auto-start on boot, automatic restarts
- **Backup system** - Daily and monthly backups with retention policies

## Non-Interactive Installation

For automated deployments, set environment variables before running:

```bash
export SELFMX_DOMAIN="mail.example.com"
export SELFMX_EMAIL="admin@example.com"
export SELFMX_PASSWORD="your-secure-password-here"
export AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
export AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
export AWS_REGION="us-east-1"
export AUTO_CONFIRM=1

curl -fsSL https://selfmx.com/install.sh | bash -s
```

## After Installation

Once complete, your SelfMX instance will be available at:

```
https://your-domain.com
```

Log in with your admin password to:

1. Create API keys for your applications
2. Add domains for email sending
3. View the audit trail

## Updating

To update an existing installation, run the installer again:

```bash
curl -fsSL https://selfmx.com/install.sh | bash -s
```

Select **[U] Update** when prompted to preserve your configuration.

## Backup & Restore

Backups run automatically at 3:00 AM daily (configurable).

**Manual backup:**
```bash
selfmx-backup
```

**Restore from backup:**
```bash
selfmx-restore /var/backups/selfmx/daily/selfmx-2024-01-15.tar.gz
```

## File Locations

| Path | Description |
|------|-------------|
| `/opt/selfmx/` | Installation directory (docker-compose.yml, .env) |
| `/var/lib/selfmx/` | Data directory (SQLite databases) |
| `/var/backups/selfmx/` | Backup directory |
| `/var/log/selfmx/` | Log files |

## Useful Commands

```bash
# View logs
docker compose -f /opt/selfmx/docker-compose.yml logs -f

# Restart services
systemctl restart selfmx

# Check status
systemctl status selfmx

# Manual backup
selfmx-backup
```

## Next Steps

- [Quick Start](/getting-started/quick-start) - Send your first email
- [Configuration](/configuration/environment) - Environment variables reference
- [Deployment Guide](/guides/deployment) - Production best practices
