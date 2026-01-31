---
title: Database Backups
description: Backup and restore procedures for SelfMX SQL Server database.
toc: true
---

## Overview

SelfMX stores domains, API keys, and audit logs in SQL Server. Regular backups protect against data loss.

## Automated Backups (Linux Install)

The standard Linux installation includes automated daily backups.

### Backup Location

```
/data/selfmx/backups/
```

Backups are named `selfmx_YYYYMMDD_HHMMSS.bak`.

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
selfmx-restore selfmx_20240115_030000.bak
```

## External SQL Server / TrueNAS

For TrueNAS or external SQL Server installations, backups must be configured manually using your SQL Server's standard backup procedures.

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
3. Restore the database from backup
4. Start the SelfMX service: `systemctl start selfmx`
5. Verify domains show correct status in admin UI

### Re-verifying Domains After Restore

If restoring to a new AWS account or after significant time:

1. Domains may show as "Verifying" while SelfMX re-checks with AWS SES
2. If DNS records are still in place, verification should complete automatically
3. If verification fails, delete and re-add the domain
