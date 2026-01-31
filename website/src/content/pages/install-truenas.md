---
title: TrueNAS Installation
description: Install SelfMX as a custom app on TrueNAS SCALE.
toc: true
---

## Overview

This guide walks you through installing SelfMX on TrueNAS SCALE using Docker Compose. SelfMX is a Resend-compatible email sending API (sending only) powered by AWS SES. It connects to an existing SQL Server instance running on your TrueNAS.

## Prerequisites

Before starting, ensure you have:

- **TrueNAS SCALE 24.10 or later** (earlier versions used Kubernetes)
- **Apps pool configured** - a storage pool designated for application data
- **SQL Server** - already running as a container on TrueNAS (install from the app catalog if needed)
- **AWS credentials** - Access Key ID, Secret Access Key, and Region for SES
- **A domain name** pointing to your TrueNAS server (for HTTPS)

### Gather Required Information

You will need these values during installation:

| Setting | Description |
|---------|-------------|
| `AWS_ACCESS_KEY_ID` | Your AWS access key for SES |
| `AWS_SECRET_ACCESS_KEY` | Your AWS secret key |
| `AWS_REGION` | AWS region where SES is configured (e.g., `us-east-1`) |
| Admin password | For browser login (min 12 characters) |
| Cloudflare credentials | Optional, for automatic DNS record creation |

### Create Dataset and Directories

Before starting the installation, create a dataset for persistent storage.

1. Navigate to **Datasets** in TrueNAS
2. Create a dataset: `apps/selfmx` (or similar, under your apps pool)
3. Create subdirectories inside the dataset using the TrueNAS Shell or SSH:

```bash
mkdir -p /mnt/YOUR_POOL/apps/selfmx/logs
```

## Step 1: Create Database and User

See **[External SQL Server Setup](/external-sql-server)** for complete instructions on creating the SelfMX database and user.

Quick reference:

```sql
CREATE DATABASE SelfMX;
GO

CREATE LOGIN selfmx WITH PASSWORD = 'YourSelfMXPassword123!';
GO

USE SelfMX;
GO

CREATE USER selfmx FOR LOGIN selfmx;
GO

ALTER ROLE db_owner ADD MEMBER selfmx;
GO
```

## Step 2: Generate Admin Password Hash

SelfMX stores admin passwords as SHA-512 crypt hashes. Generate one:

```bash
openssl passwd -6 "YourSecurePassword"
```

Save the hash (starts with `$6$...`) for the Docker Compose configuration.

## Step 3: Open the YAML Installation Wizard

1. Navigate to **Apps** > **Discover Apps**
2. Click the three-dot menu in the top right
3. Select **Install via YAML**

## Step 4: Configure the Application

### Application Name

Enter: `selfmx`

### Docker Compose Configuration

Paste the following YAML, replacing the placeholder values:

```yaml
services:
  selfmx:
    image: ghcr.io/aduggleby/selfmx:latest
    ports:
      - "8080:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - App__Fqdn=YOUR_DOMAIN
      - App__AdminPasswordHash=YOUR_SHA512_HASH
      - ConnectionStrings__DefaultConnection=Server=YOUR_TRUENAS_IP,1433;Database=SelfMX;User Id=selfmx;Password=YOUR_SELFMX_PASSWORD;TrustServerCertificate=true;Encrypt=True
      - Aws__Region=YOUR_AWS_REGION
      - Aws__AccessKeyId=YOUR_AWS_ACCESS_KEY_ID
      - Aws__SecretAccessKey=YOUR_AWS_SECRET_ACCESS_KEY
    volumes:
      - /mnt/YOUR_POOL/apps/selfmx/logs:/app/logs
    restart: unless-stopped
```

### Values to Replace

| Placeholder | Replace With |
|-------------|--------------|
| `YOUR_DOMAIN` | Your SelfMX domain (e.g., `mail.example.com`) |
| `YOUR_SHA512_HASH` | The SHA-512 hash from Step 2 (starts with `$6$...`) |
| `YOUR_TRUENAS_IP` | Your TrueNAS host IP address (e.g., `192.168.1.100`) |
| `YOUR_SELFMX_PASSWORD` | The password you set for the `selfmx` login in Step 1 |
| `YOUR_POOL` | Your TrueNAS pool name (e.g., `tank`, `data`) |
| `YOUR_AWS_REGION` | AWS region (e.g., `us-east-1`) |
| `YOUR_AWS_ACCESS_KEY_ID` | Your AWS access key |
| `YOUR_AWS_SECRET_ACCESS_KEY` | Your AWS secret key |

### Optional Settings

These environment variables have sensible defaults but can be customized:

| Variable | Default | Description |
|----------|---------|-------------|
| `App__SessionExpirationDays` | `30` | Admin session duration in days |
| `App__MaxLoginAttemptsPerMinute` | `5` | Login rate limit |
| `App__MaxApiRequestsPerMinute` | `100` | API rate limit per key |
| `App__VerificationTimeout` | `72:00:00` | Domain verification timeout |
| `App__VerificationPollInterval` | `00:05:00` | Verification check interval |
| `Cloudflare__ApiToken` | | For automatic DNS record creation |
| `Cloudflare__ZoneId` | | Cloudflare zone ID |

## Step 5: Install

Click **Install** to deploy the container. TrueNAS will pull the Docker image and start SelfMX.

## Step 6: Configure Reverse Proxy

For HTTPS access, configure a reverse proxy.

### Option A: TrueNAS Built-in or Traefik

If your TrueNAS has a public IP:

1. Install Traefik from the TrueNAS app catalog
2. Configure it to proxy `your-domain.com` > `localhost:8080`

### Option B: External Reverse Proxy

If using Caddy, nginx, or another external proxy:

```
# Example Caddy configuration
your-domain.com {
    reverse_proxy YOUR_TRUENAS_IP:8080
}
```

## Verification

1. Access the SelfMX dashboard at `http://YOUR_TRUENAS_IP:8080` (or your HTTPS domain)
2. Log in with your admin password
3. Create an API key
4. Add a domain for email sending

## Updating SelfMX

To update to a new version:

1. Navigate to **Apps** > **Installed Applications**
2. Click on `selfmx`
3. Click **Edit** > update the image tag or use `latest` > **Save**

The container will restart with the new version.

## Backups

For backup and restore procedures, see **[Database Backups](/backups)**.

## Troubleshooting

### View Logs

1. Navigate to **Apps** > **Installed Applications**
2. Click on `selfmx`
3. Click **Logs** to view the container output

### Container Won't Start

Check that:
- All dataset paths exist and are accessible
- Environment variables are correctly formatted (no extra spaces)
- The SHA-512 hash doesn't have special characters that need escaping

### Database Connection Failed

1. Verify the TrueNAS IP and port are correct
2. Test the connection: `nc -zv YOUR_TRUENAS_IP 1433`
3. Verify the `selfmx` login was created correctly
4. Check the password matches what you set in Step 1

### Permission Denied on Mounted Volumes

TrueNAS may need ACL configuration:

1. Navigate to the dataset in **Datasets**
2. Click **Edit Permissions**
3. Add an ACL entry for UID 0 (root) with full access

## More Information

- [Main Installation Documentation](/install)
- [TrueNAS Custom Apps Documentation](https://apps.truenas.com/managing-apps/installing-custom-apps/)
