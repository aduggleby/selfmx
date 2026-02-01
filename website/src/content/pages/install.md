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
- **SQL Server Database**: Containerized SQL Server 2022 with automated backups.
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
| RAM | 4GB (SQL Server requires 2GB minimum) |
| Disk | 10GB free space |
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
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Aws__Region` | AWS region (e.g., `us-east-1`) |
| `Aws__AccessKeyId` | AWS access key ID |
| `Aws__SecretAccessKey` | AWS secret access key |

### Optional Settings

| Variable | Default | Description |
|----------|---------|-------------|
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

## Using External SQL Server

To use an existing SQL Server instead of the containerized one, see **[External SQL Server Setup](/external-sql-server)**.

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
| `/data/selfmx/sqlserver/` | SQL Server database files |
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
docker logs selfmx-sqlserver
```

### Database Connection Failed

1. Verify SQL Server is running: `docker ps | grep sqlserver`
2. Check SQL Server has enough memory (requires 2GB minimum)
3. Test connection with sqlcmd

### AWS SES Errors

1. Verify AWS credentials are correct (see [AWS Setup Guide](/aws-setup))
2. Check SES is enabled in your region
3. Verify sending domain is verified in SES
4. Ensure your account is out of sandbox mode for production use

### Domain Verification Stuck

1. Check Cloudflare credentials if using automatic DNS
2. Manually add DNS records shown in the admin UI
3. Verify DNS propagation: `dig TXT _amazonses.yourdomain.com`
