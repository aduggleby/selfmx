---
title: Database Backups
description: Backup and restore procedures for the SelfMX PostgreSQL or SQL Server database.
toc: true
---

## Overview

SelfMX stores domains, API keys, and audit logs in PostgreSQL (or SQL Server on older installs). Regular backups protect against data loss.

## Automated Backups (Linux Install)

The standard Linux installation includes automated daily backups.

### Backup Location

```
/data/selfmx/backups/
```

Backups are named `selfmx_YYYYMMDD_HHMMSS.dump` (PostgreSQL custom format, created with `pg_dump -Fc`). Installs that run SQL Server use `.bak` files instead.

### Manual Backup

Run the backup command:

```bash
selfmx-backup
```

### Restore from Backup

List available backups and restore:

```bash
# List backups
ls /data/selfmx/backups/

# Restore specific backup
selfmx-restore selfmx_20240115_030000.dump
```

The restore script stops the app, runs `pg_restore --clean --if-exists`, and starts the app again.

## External PostgreSQL / TrueNAS

For TrueNAS or external PostgreSQL installations, backups must be configured manually. The standard tools work:

```bash
# Backup
pg_dump -h YOUR_HOST -U selfmx -d selfmx -Fc -f selfmx_backup.dump

# Restore (stop SelfMX first)
pg_restore -h YOUR_HOST -U selfmx -d selfmx --clean --if-exists --no-owner selfmx_backup.dump
```

The dump includes the Hangfire job tables (schema `hangfire`) stored in the same database.

## SQL Server (Alternative)

SQL Server installs created by the installer use the same `selfmx-backup` and `selfmx-restore` commands, with `.bak` files (`BACKUP DATABASE` / `RESTORE DATABASE`). For external SQL Server instances, backups must be configured manually using your SQL Server's standard backup procedures.

## Backup Strategy

### Recommended Schedule

| Backup Type | Frequency | Retention |
|-------------|-----------|-----------|
| Full backup | Daily | 7 days |
| Weekly backup | Weekly | 4 weeks |
| Monthly backup | Monthly | 12 months |

### What Gets Backed Up

- **Domains** - All registered sending domains and DNS records
- **API Keys** - Encrypted API key hashes and permissions
- **Audit Logs** - Email send history and API activity

### What Is NOT Backed Up

- **AWS SES state** - Domain verification status lives in AWS

> **Note:** Sent emails ARE stored in the database and included in backups. Configure retention with `App__SentEmailRetentionDays` (default: keep forever).

## Disaster Recovery

### Complete Restore Procedure

1. Install SelfMX on new server (without starting the app)
2. Stop the SelfMX service: `systemctl stop selfmx`
3. Restore the database from backup (`selfmx-restore <file>` on installer-managed installs)
4. Start the SelfMX service: `systemctl start selfmx`
5. Verify domains show correct status in admin UI

### Re-verifying Domains After Restore

If restoring to a new AWS account or after significant time:

1. Domains may show as "Verifying" while SelfMX re-checks with AWS SES
2. If DNS records are still in place, verification should complete automatically
3. If verification fails, delete and re-add the domain

## Moving to Another Server

To move SelfMX to a new server or from SQL Server to PostgreSQL, use the built-in export and import. See [Moving to Another Server](/migrate).
