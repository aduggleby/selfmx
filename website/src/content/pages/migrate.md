---
title: Moving to Another Server
description: Export a SelfMX instance and import it into a new installation, including from SQL Server to PostgreSQL.
toc: true
---

## Overview

SelfMX can export its configuration to a single file and import it into a fresh installation. Use it to move to a new server or to switch from SQL Server to PostgreSQL.

The export contains:

- Domains, including their verification state and DNS records
- API keys (stored as hashes, so existing keys keep working without any change in client apps)
- API key domain scopes and archived revoked keys
- The audit log

Sent email history and background job state are **not** included.

## Before You Start

- Both instances must run a SelfMX version with export/import (the same version is recommended).
- Use the **same AWS account and region** on the new server. SES domain identities live in AWS, so imported domains stay verified.
- The export file contains API key hashes. Treat it like a secret and delete it after the move.

## 1. Export

In the admin UI of the old instance, open **Data** and click **Download export**. This downloads `selfmx-export-<date>.json.gz`.

Or with curl and the admin password:

```bash
curl -c cookies -H 'Content-Type: application/json' -d '{"password":"..."}' https://old.example.com/admin/login
curl -b cookies -o selfmx-export.json.gz https://old.example.com/admin/export
```

## 2. Import

Install SelfMX on the new server, sign in with the admin password, open **Data** and import the file. The import is refused if the new instance already has domains or API keys, so do this before creating anything.

```bash
curl -c cookies -H 'Content-Type: application/json' -d '{"password":"..."}' https://new.example.com/admin/login
curl -b cookies -H 'Content-Type: application/gzip' --data-binary @selfmx-export.json.gz https://new.example.com/admin/import
```

## 3. Switch Over

1. Check that the domains show as verified and send a test email from the new instance.
2. Point your SelfMX hostname's DNS at the new server.
3. Keep the old instance until all client apps send through the new one, then retire it.

Emails sent through the old instance after the export stay in its history only.
