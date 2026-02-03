---
title: API Reference
description: Resend-compatible REST API for sending emails (sending only) and managing domains.
toc: true
---

## Overview

SelfMX provides a Resend-compatible REST API for sending transactional emails (sending only, no receiving or webhooks yet). All endpoints use JSON for request and response bodies.

## Authentication

All API requests require authentication via Bearer token:

```bash
curl https://mail.yourdomain.com/v1/emails \
  -H "Authorization: Bearer re_xxxxxxxxxxxx"
```

API keys are created in the admin UI and use the Resend format: `re_` followed by 28 random characters.

## Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/health` | GET | No | Health check |
| `/v1/system/status` | GET | No | System status check |
| `/v1/system/version` | GET | No | Version and build info |
| `/v1/system/logs` | GET | Admin | Application logs |
| `/v1/emails` | POST | API Key | Send email |
| `/v1/domains` | GET | API Key | List domains |
| `/v1/domains` | POST | API Key | Create domain |
| `/v1/domains/{id}` | GET | API Key | Get domain |
| `/v1/domains/{id}` | DELETE | API Key | Delete domain |
| `/v1/domains/{id}/verify` | POST | API Key | Trigger verification check |
| `/v1/domains/{id}/test-email` | POST | API Key | Send test email |
| `/v1/api-keys` | GET | Admin | List API keys |
| `/v1/api-keys` | POST | Admin | Create API key |
| `/v1/api-keys/revoked` | GET | Admin | List archived API keys |
| `/v1/api-keys/{id}` | DELETE | Admin | Revoke API key |
| `/v1/sent-emails` | GET | Admin | List sent emails |
| `/v1/sent-emails/{id}` | GET | Admin | Get sent email details |
| `/v1/audit` | GET | Admin | Audit logs |
| `/hangfire` | GET | Admin | Background jobs dashboard |

## Send Email

Send a transactional email.

**POST** `/v1/emails`

### Request

```json
{
  "from": "Sender Name <sender@yourdomain.com>",
  "to": ["recipient@example.com"],
  "cc": ["cc@example.com"],
  "bcc": ["bcc@example.com"],
  "reply_to": "reply@yourdomain.com",
  "subject": "Email Subject",
  "html": "<p>HTML content</p>",
  "text": "Plain text content"
}
```

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `from` | string | Yes | Sender email (must be verified domain) |
| `to` | string or array | Yes | Recipient email(s) |
| `subject` | string | Yes | Email subject line |
| `html` | string | No* | HTML body content |
| `text` | string | No* | Plain text body |
| `cc` | array | No | Carbon copy recipients |
| `bcc` | array | No | Blind carbon copy recipients |
| `reply_to` | string | No | Reply-to address |

*At least one of `html` or `text` is required.

### Response

```json
{
  "id": "msg_xxxxxxxxxxxx"
}
```

### Example

```bash
curl -X POST https://mail.yourdomain.com/v1/emails \
  -H "Authorization: Bearer re_xxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d '{
    "from": "hello@yourdomain.com",
    "to": "recipient@example.com",
    "subject": "Hello from SelfMX",
    "html": "<h1>Welcome!</h1><p>Your first email from SelfMX.</p>"
  }'
```

## List Domains

Get all domains for the authenticated API key.

**GET** `/v1/domains`

### Response

```json
{
  "data": [
    {
      "id": "d5f2a3b1-...",
      "name": "yourdomain.com",
      "status": "verified",
      "createdAt": "2024-01-15T10:30:00Z",
      "lastCheckedAt": "2024-01-15T10:35:00Z",
      "nextCheckAt": null
    }
  ]
}
```

### Domain Status Values

| Status | Description |
|--------|-------------|
| `pending` | Domain added, waiting for setup job |
| `verifying` | DNS records created, waiting for verification |
| `verified` | Domain verified and ready for sending |
| `failed` | Verification failed |

## Create Domain

Add a domain for email sending.

**POST** `/v1/domains`

### Request

```json
{
  "name": "example.com"
}
```

### Response

```json
{
  "id": "d5f2a3b1-...",
  "name": "example.com",
  "status": "pending",
  "createdAt": "2024-01-15T10:30:00Z",
  "lastCheckedAt": null,
  "nextCheckAt": null
}
```

### Example

```bash
curl -X POST https://mail.yourdomain.com/v1/domains \
  -H "Authorization: Bearer re_xxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d '{"name": "example.com"}'
```

## Get Domain

Get details for a specific domain including DNS records.

**GET** `/v1/domains/{id}`

### Response

```json
{
  "id": "d5f2a3b1-...",
  "name": "example.com",
  "status": "verifying",
  "dnsRecords": [
    {
      "type": "CNAME",
      "name": "token1._domainkey.example.com",
      "value": "token1.dkim.amazonses.com"
    },
    {
      "type": "CNAME",
      "name": "token2._domainkey.example.com",
      "value": "token2.dkim.amazonses.com"
    },
    {
      "type": "CNAME",
      "name": "token3._domainkey.example.com",
      "value": "token3.dkim.amazonses.com"
    }
  ],
  "createdAt": "2024-01-15T10:30:00Z",
  "lastCheckedAt": "2024-01-15T10:32:00Z",
  "nextCheckAt": "2024-01-15T10:35:00Z"
}
```

## Delete Domain

Remove a domain from SelfMX and AWS SES.

**DELETE** `/v1/domains/{id}`

### Response

```
204 No Content
```

## Verify Domain

Manually trigger a verification check for a domain. Only works for domains in `Verifying` status.

**POST** `/v1/domains/{id}/verify`

### Response

Returns the updated domain with current verification status:

```json
{
  "id": "d5f2a3b1-...",
  "name": "example.com",
  "status": "verified",
  "createdAt": "2024-01-15T10:30:00Z",
  "verifiedAt": "2024-01-15T10:35:00Z",
  "lastCheckedAt": "2024-01-15T10:35:00Z",
  "nextCheckAt": null
}
```

### Example

```bash
curl -X POST https://mail.yourdomain.com/v1/domains/{id}/verify \
  -H "Authorization: Bearer re_xxxxxxxxxxxx"
```

### Notes

- Returns `400 Bad Request` if the domain is not in `Verifying` status
- Use this to immediately check verification instead of waiting for the next scheduled check (every 5 minutes)
- After verification succeeds, `status` becomes `verified` and `nextCheckAt` becomes `null`

## Send Test Email

Send a test email from a verified domain. Useful for verifying domain configuration.

**POST** `/v1/domains/{id}/test-email`

### Request

```json
{
  "senderPrefix": "test",
  "to": "recipient@example.com",
  "subject": "Test Email",
  "text": "This is a test email from SelfMX."
}
```

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `senderPrefix` | string | Yes | Local part of sender address (e.g., `test` becomes `test@yourdomain.com`) |
| `to` | string | Yes | Recipient email address |
| `subject` | string | Yes | Email subject line |
| `text` | string | Yes | Plain text body content |

### Response

```json
{
  "id": "msg_xxxxxxxxxxxx"
}
```

### Example

```bash
curl -X POST https://mail.yourdomain.com/v1/domains/{id}/test-email \
  -H "Authorization: Bearer re_xxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d '{
    "senderPrefix": "test",
    "to": "recipient@example.com",
    "subject": "Test Email",
    "text": "This is a test email from SelfMX."
  }'
```

### Notes

- Domain must be in `Verified` status
- Sender prefix must contain only alphanumeric characters, dots, underscores, and hyphens
- The full sender address is constructed as `{senderPrefix}@{domainName}`

## List API Keys (Admin)

Get all API keys. Requires admin authentication.

**GET** `/v1/api-keys`

### Response

```json
{
  "data": [
    {
      "id": "k5f2a3b1-...",
      "name": "Production",
      "domains": ["example.com", "app.example.com"],
      "createdAt": "2024-01-15T10:30:00Z"
    }
  ]
}
```

## Create API Key (Admin)

Create a new API key. Requires admin authentication.

**POST** `/v1/api-keys`

### Request

```json
{
  "name": "Production",
  "domainIds": ["d5f2a3b1-...", "d5f2a3b2-..."]
}
```

### Response

```json
{
  "id": "k5f2a3b1-...",
  "name": "Production",
  "key": "re_xxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

> **Important**: The `key` field is only returned once at creation time. Store it securely.

## Revoke API Key (Admin)

Revoke an API key. Requires admin authentication.

**DELETE** `/v1/api-keys/{id}`

### Response

```
204 No Content
```

## List Archived API Keys (Admin)

Get archived (previously revoked) API keys. Revoked API keys are automatically archived after 90 days by a daily cleanup job. Requires admin authentication.

**GET** `/v1/api-keys/revoked`

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page |

### Response

```json
{
  "data": [
    {
      "id": "k5f2a3b1-...",
      "name": "Old Production Key",
      "keyPrefix": "re_abc123",
      "isAdmin": false,
      "createdAt": "2024-01-15T10:30:00Z",
      "revokedAt": "2024-04-15T10:30:00Z",
      "archivedAt": "2024-07-15T04:00:00Z",
      "lastUsedAt": "2024-04-10T08:15:00Z",
      "domainIds": ["d5f2a3b1-...", "d5f2a3b2-..."]
    }
  ],
  "page": 1,
  "limit": 20,
  "total": 5
}
```

### Notes

- Revoked keys are archived (moved to a separate table) after 90 days
- The cleanup job runs daily at 4 AM UTC
- Archived keys preserve historical data for audit purposes

## List Sent Emails (Admin)

Get sent emails with cursor-based pagination and filtering. Requires admin authentication.

**GET** `/v1/sent-emails`

### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `domainId` | string | Filter by domain ID |
| `from` | string | Filter by sender address (partial match) |
| `to` | string | Filter by recipient address (partial match) |
| `cursor` | string | Cursor for next page |
| `pageSize` | integer | Items per page (default: 50) |

### Response

```json
{
  "data": [
    {
      "id": "e5f2a3b1-...",
      "messageId": "msg_xxxxxxxxxxxx",
      "sentAt": "2024-01-15T10:30:00Z",
      "fromAddress": "hello@yourdomain.com",
      "to": ["recipient@example.com"],
      "subject": "Hello from SelfMX",
      "domainId": "d5f2a3b1-...",
      "apiKeyId": "k5f2a3b1-...",
      "apiKeyName": "Production"
    }
  ],
  "nextCursor": "eyJpZCI6IjEyMyJ9",
  "hasMore": true
}
```

### Pagination

Use cursor-based pagination for large datasets:

```bash
# First page
curl https://mail.yourdomain.com/v1/sent-emails?pageSize=50

# Next page (use nextCursor from previous response)
curl https://mail.yourdomain.com/v1/sent-emails?cursor=eyJpZCI6IjEyMyJ9
```

## Get Sent Email (Admin)

Get details of a specific sent email including the full body. Requires admin authentication.

**GET** `/v1/sent-emails/{id}`

### Response

```json
{
  "id": "e5f2a3b1-...",
  "messageId": "msg_xxxxxxxxxxxx",
  "sentAt": "2024-01-15T10:30:00Z",
  "fromAddress": "hello@yourdomain.com",
  "to": ["recipient@example.com"],
  "cc": ["cc@example.com"],
  "replyTo": "reply@yourdomain.com",
  "subject": "Hello from SelfMX",
  "htmlBody": "<p>HTML content</p>",
  "textBody": "Plain text content",
  "domainId": "d5f2a3b1-...",
  "apiKeyId": "k5f2a3b1-...",
  "apiKeyName": "Production"
}
```

### Notes

- `apiKeyId` and `apiKeyName` identify which API key was used to send the email
- `apiKeyName` is `null` if the API key has been deleted
- Both fields are `null` for emails sent via admin session authentication

## Audit Logs (Admin)

Get audit logs. Requires admin authentication.

**GET** `/v1/audit`

### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | datetime | Start date (ISO 8601) |
| `to` | datetime | End date (ISO 8601) |
| `page` | integer | Page number (default: 1) |
| `pageSize` | integer | Items per page (default: 50) |

### Response

```json
{
  "data": [
    {
      "id": "a5f2a3b1-...",
      "apiKeyId": "k5f2a3b1-...",
      "action": "SendEmail",
      "details": "{...}",
      "createdAt": "2024-01-15T10:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1234
}
```

## Error Responses

All errors return a consistent JSON format:

```json
{
  "statusCode": 400,
  "message": "Domain not verified",
  "name": "validation_error"
}
```

### Common Error Codes

| Code | Description |
|------|-------------|
| `400` | Bad Request - Invalid input |
| `401` | Unauthorized - Invalid or missing API key |
| `403` | Forbidden - Key doesn't have access to domain |
| `404` | Not Found - Resource doesn't exist |
| `429` | Too Many Requests - Rate limit exceeded |
| `500` | Internal Server Error |

## Rate Limits

API requests are rate limited per API key.

| Header | Description |
|--------|-------------|
| `X-RateLimit-Limit` | Requests allowed per minute |
| `X-RateLimit-Remaining` | Requests remaining |
| `X-RateLimit-Reset` | Unix timestamp when limit resets |

Default: 100 requests per minute per API key.

## Health Check

Check if the API is running.

**GET** `/health`

### Response

```json
{
  "status": "Healthy"
}
```

## System Status

Check system configuration and connectivity. Returns AWS and database health status. Use this to verify your deployment is properly configured.

**GET** `/v1/system/status`

### Response

```json
{
  "healthy": true,
  "issues": [],
  "timestamp": "2026-02-02T10:30:00Z"
}
```

When configuration issues are detected:

```json
{
  "healthy": false,
  "issues": [
    "AWS SES: Access Denied",
    "AWS: Region not configured (Aws__Region)"
  ],
  "timestamp": "2026-02-02T10:30:00Z"
}
```

### Checks Performed

| Check | Description |
|-------|-------------|
| AWS SES | Verifies credentials can access SES account |
| Database | Tests database connectivity |
| AWS Config | Validates Region, AccessKeyId, SecretAccessKey are set |

## System Version

Get the API version and build information. Useful for debugging and verifying deployments.

**GET** `/v1/system/version`

### Response

```json
{
  "version": "0.9.38.0",
  "informationalVersion": "0.9.38+3d4f16e",
  "buildDate": "2026-02-03T13:21:28Z",
  "environment": "Production"
}
```

### Fields

| Field | Description |
|-------|-------------|
| `version` | Assembly version (Major.Minor.Patch.Revision) |
| `informationalVersion` | Full version including git commit hash |
| `buildDate` | Build timestamp (UTC) |
| `environment` | ASP.NET environment (Development, Production) |

## System Logs (Admin)

Get recent application logs for remote diagnostics. Requires admin authentication.

**GET** `/v1/system/logs`

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | integer | 1000 | Number of log entries to return (max 2000) |
| `level` | string | - | Filter by log level (e.g., `Error`, `Warning`, `Information`) |
| `category` | string | - | Filter by logger category (partial match) |

### Response

```json
{
  "count": 150,
  "logs": [
    {
      "timestamp": "2026-02-03T14:21:05Z",
      "level": "Information",
      "category": "SelfMX.Api.Services.SesService",
      "message": "Email sent successfully to recipient@example.com",
      "exception": null
    },
    {
      "timestamp": "2026-02-03T14:20:58Z",
      "level": "Error",
      "category": "Microsoft.AspNetCore.Server.Kestrel",
      "message": "Connection reset by peer",
      "exception": "System.IO.IOException: Connection reset..."
    }
  ]
}
```

### Example

```bash
# Get last 100 error logs
curl "https://mail.yourdomain.com/v1/system/logs?count=100&level=Error" \
  -H "Cookie: auth_session=your_session_cookie"

# Get logs from a specific category
curl "https://mail.yourdomain.com/v1/system/logs?category=SesService" \
  -H "Cookie: auth_session=your_session_cookie"
```

### Notes

- Logs are stored in memory (circular buffer of 2000 entries)
- Logs are lost on application restart
- Captures all log levels (Debug and above)
- Useful for debugging issues without SSH access

## Background Jobs Dashboard

View and manage Hangfire background jobs. Requires admin authentication.

**GET** `/hangfire`

Access the Hangfire dashboard to monitor:

- Recurring jobs (domain verification polling)
- Failed jobs and retry status
- Job processing metrics
- Queue status

The dashboard is available in all environments (development and production) and requires admin cookie authentication.
