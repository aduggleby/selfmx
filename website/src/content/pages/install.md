---
title: SelfMX Installation
description: Self-hosted email API powered by AWS SES with Cloudflare DNS integration.
toc: true
---

## Overview

SelfMX is a self-hosted email sending platform providing a Resend-compatible API (sending emails only) powered by AWS SES. It simplifies domain verification with optional Cloudflare DNS integration.

### Features

- **Resend-Compatible API**: Drop-in replacement for Resend's email sending API (sending only).
- **AWS SES Integration**: Reliable email delivery powered by Amazon Simple Email Service.
- **Cloudflare DNS Integration**: Optional automatic DNS record creation for domain verification.
- **PostgreSQL Database**: Containerized PostgreSQL 17 with automated backups (SQL Server is supported as an alternative).
- **Automatic HTTPS**: Caddy reverse proxy with Let's Encrypt certificates.

## Installation

### Linux/Ubuntu Server

Run the installer from your local machine:

```bash
curl -fsSL https://selfmx.com/install.sh | bash -s user@your-server-ip
```

Or SSH into your server and run directly:

```bash
curl -fsSL https://selfmx.com/install.sh | sudo bash
```

### TrueNAS SCALE

For TrueNAS SCALE installations, see the dedicated guide: **[TrueNAS Installation](/install-truenas)**

## Requirements

| Requirement | Minimum |
|-------------|---------|
| OS | Ubuntu 22.04 LTS or newer |
| RAM | 1GB (4GB if you use SQL Server) |
| Disk | 5GB free space (10GB if you use SQL Server) |
| Ports | 80 and 443 available |
| AWS | Account with SES access ([setup guide](/aws-setup)) |

## Configuration Prompts

The interactive installer prompts for:

| Setting | Description |
|---------|-------------|
| Domain name | Where SelfMX will be hosted (e.g., `mail.example.com`) |
| Admin password | For logging into the admin UI (min 12 characters) |
| AWS credentials | Access Key ID, Secret, and Region for SES ([setup guide](/aws-setup)) |
| Cloudflare credentials | Optional, for automatic DNS record creation |

## Non-Interactive Installation

Set environment variables before running for automated deployments:

```bash
# === Required: Server Configuration ===
export SELFMX_DOMAIN="mail.example.com"       # FQDN where SelfMX is hosted
export SELFMX_EMAIL="admin@example.com"       # Email for Let's Encrypt certificates
export SELFMX_PASSWORD="your-secure-password" # Admin UI password (min 12 chars)

# === Required: AWS SES Credentials (see /aws-setup for how to create these) ===
export AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
export AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
export AWS_REGION="us-east-1"

# === Skip confirmation prompts ===
export AUTO_CONFIRM=1

curl -fsSL https://selfmx.com/install.sh | sudo bash
```

The installer generates the admin password hash automatically from `SELFMX_PASSWORD`.

## Configuration Reference

All configuration is via environment variables.

### Required Settings

| Variable | Description |
|----------|-------------|
| `App__Fqdn` | FQDN where SelfMX is hosted (e.g., `mail.example.com`) |
| `App__AdminPasswordHash` | SHA-512 hash of admin password |
| `ConnectionStrings__DefaultConnection` | PostgreSQL (`Host=...`) or SQL Server (`Server=...`) connection string |
| `Aws__Region` | AWS region (e.g., `us-east-1`) |
| `Aws__AccessKeyId` | AWS access key ID |
| `Aws__SecretAccessKey` | AWS secret access key |

### Optional Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `Database__Provider` | inferred | `Postgres` or `SqlServer`. If unset, inferred from the connection string (`Host=` is Postgres, `Server=` or `Data Source=` is SQL Server) |
| `App__HangfireWorkers` | CPU count x 2 (max 20) | Background job worker count |
| `App__SessionExpirationDays` | `30` | Admin session duration |
| `App__MaxLoginAttemptsPerMinute` | `5` | Login rate limit |
| `App__MaxApiRequestsPerMinute` | `100` | API rate limit per key |
| `App__VerificationTimeout` | `72:00:00` | Timeout before marking domain as failed |
| `App__VerificationPollInterval` | `00:05:00` | How often to check domain status |
| `Cloudflare__ApiToken` | | API token with DNS edit permissions |
| `Cloudflare__ZoneId` | | Zone ID for your domain |

### Generating Admin Password Hash

Generate a SHA-512 hash for your admin password:

```bash
openssl passwd -6 "YourSecurePassword"
```

This produces a hash like `$6$salt$hash...` which you set as `App__AdminPasswordHash`.

**Docker Compose note:** The hash contains `$` characters. Wrap the value in single quotes to prevent variable interpolation:

```yaml
- 'App__AdminPasswordHash=$6$salt$hash...'
```

## Using an External Database

The installer runs PostgreSQL 17 in a container. To use an existing database instead:

- **PostgreSQL**: see **[External PostgreSQL Setup](/external-postgres)**
- **SQL Server**: see **[External SQL Server Setup](/external-sql-server)**

### Existing SQL Server Installs

Installations created before PostgreSQL became the default keep running on SQL Server. Re-running the installer detects this (via `/data/selfmx/sqlserver` or `MSSQL_SA_PASSWORD` in `/opt/selfmx/.env`) and leaves your database untouched. Migrating to PostgreSQL will be possible with SelfMX's upcoming export/import feature.

## Server Management

```bash
# View logs
docker compose -f /opt/selfmx/docker-compose.yml logs -f

# Restart services
systemctl restart selfmx

# Check status
systemctl status selfmx
```

For backup and restore procedures, see **[Database Backups](/backups)**.

## File Locations

| Path | Description |
|------|-------------|
| `/opt/selfmx/` | Installation directory (docker-compose.yml, .env) |
| `/data/selfmx/postgres/` | PostgreSQL database files (`/data/selfmx/sqlserver/` on SQL Server installs) |
| `/data/selfmx/backups/` | Database backups |
| `/data/selfmx/logs/` | Application logs |

## Updating

Run the installer again and select **[U] Update**:

```bash
curl -fsSL https://selfmx.com/install.sh | sudo bash
```

This preserves your configuration and database while pulling the latest image.

## Troubleshooting

### Container Won't Start

Check logs for errors:

```bash
docker logs selfmx-app
docker logs selfmx-postgres   # selfmx-sqlserver on SQL Server installs
```

### Database Connection Failed

1. Verify the database is running: `docker ps | grep -E "postgres|sqlserver"`
2. Check the database container is healthy: `docker inspect --format '{{.State.Health.Status}}' selfmx-postgres`
3. Test the connection: `docker exec -it selfmx-postgres psql -U selfmx -d selfmx` (SQL Server installs: use sqlcmd, and make sure it has at least 2GB of memory)

### AWS SES Errors

1. Verify AWS credentials are correct (see [AWS Setup Guide](/aws-setup))
2. Check SES is enabled in your region
3. Verify sending domain is verified in SES
4. Ensure your account is out of sandbox mode for production use

### Domain Verification Stuck

1. Check Cloudflare credentials if using automatic DNS
2. Manually add DNS records shown in the admin UI
3. Verify DNS propagation: `dig TXT _amazonses.yourdomain.com`
