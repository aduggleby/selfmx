---
layout: ../../layouts/DocsLayout.astro
title: Environment Variables
description: Complete reference for SelfMX environment variables.
---

All SelfMX configuration is done through environment variables.

## Core Settings

### DATABASE_URL

**Required.** PostgreSQL connection string.

```bash
DATABASE_URL=postgres://user:password@localhost:5432/selfmx
```

Format: `postgres://[user]:[password]@[host]:[port]/[database]`

### API_KEY

**Required.** API authentication key for the default tenant.

```bash
API_KEY=your-secure-api-key-here
```

Generate a secure key:

```bash
openssl rand -hex 32
```

### PORT

HTTP API port. Default: `8080`

```bash
PORT=8080
```

### HOST

Bind address for the HTTP server. Default: `0.0.0.0`

```bash
HOST=0.0.0.0
```

## Logging

### LOG_LEVEL

Logging verbosity. Options: `debug`, `info`, `warn`, `error`. Default: `info`

```bash
LOG_LEVEL=info
```

### LOG_FORMAT

Log output format. Options: `json`, `text`. Default: `json`

```bash
LOG_FORMAT=json
```

## Security

### CORS_ORIGINS

Allowed CORS origins (comma-separated). Default: `*`

```bash
CORS_ORIGINS=https://app.example.com,https://admin.example.com
```

### RATE_LIMIT_REQUESTS

Maximum requests per minute per API key. Default: `100`

```bash
RATE_LIMIT_REQUESTS=100
```

### RATE_LIMIT_WINDOW

Rate limit window in seconds. Default: `60`

```bash
RATE_LIMIT_WINDOW=60
```

## Email Storage

### SENT_EMAIL_RETENTION_DAYS

Days to retain sent email records. Set to `0` or leave unset to keep forever.

```bash
App__SentEmailRetentionDays=30
```

A cleanup job runs daily at 3 AM to delete emails older than the retention period.

## Queue Settings

### QUEUE_WORKERS

Number of email processing workers. Default: `4`

```bash
QUEUE_WORKERS=4
```

### QUEUE_RETRY_ATTEMPTS

Maximum retry attempts for failed emails. Default: `3`

```bash
QUEUE_RETRY_ATTEMPTS=3
```

### QUEUE_RETRY_DELAY

Delay between retries in seconds. Default: `60`

```bash
QUEUE_RETRY_DELAY=60
```

## Example Configuration

Complete example for production:

```bash
# Database
DATABASE_URL=postgres://selfmx:secure-password@db.example.com:5432/selfmx

# API
API_KEY=your-secure-api-key
PORT=8080
HOST=0.0.0.0

# Logging
LOG_LEVEL=info
LOG_FORMAT=json

# Security
CORS_ORIGINS=https://app.example.com
RATE_LIMIT_REQUESTS=100
RATE_LIMIT_WINDOW=60

# Queue
QUEUE_WORKERS=8
QUEUE_RETRY_ATTEMPTS=5
QUEUE_RETRY_DELAY=120

# SMTP (see SMTP Settings page)
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USER=apikey
SMTP_PASS=your-smtp-password
SMTP_TLS=true
```

## Next Steps

- [SMTP Settings](/configuration/smtp) - Email delivery configuration
- [Database Options](/configuration/database) - Database tuning
