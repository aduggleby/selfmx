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
curl https://mail.yourdomain.com/emails \
  -H "Authorization: Bearer re_xxxxxxxxxxxx"
```

API keys are created in the admin UI and use the Resend format: `re_` followed by 28 random characters.

## Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/health` | GET | No | Health check |
| `/system/status` | GET | No | System status check |
| `/system/version` | GET | No | Version and build info |
| `/system/logs` | GET | Admin | Application logs |
| `/emails` | POST | API Key | Send email |
| `/emails/{id}` | GET | API Key | Get sent email |
| `/emails` | GET | API Key | List sent emails |
| `/emails/batch` | POST | API Key | Send batch emails |
| `/domains` | GET | API Key | List domains |
| `/domains` | POST | API Key | Create domain |
| `/domains/{id}` | GET | API Key | Get domain |
| `/domains/{id}` | DELETE | API Key | Delete domain |
| `/domains/{id}/verify` | POST | API Key | Trigger verification check |
| `/domains/{id}/test-email` | POST | API Key | Send test email |
| `/tokens/me` | GET | API Key | Token introspection |
| `/api-keys` | GET | Admin | List API keys |
| `/api-keys` | POST | Admin | Create API key |
| `/api-keys/revoked` | GET | Admin | List archived API keys |
| `/api-keys/{id}` | DELETE | Admin | Revoke API key |
| `/sent-emails` | GET | Admin | List sent emails |
| `/sent-emails/{id}` | GET | Admin | Get sent email details |
| `/audit` | GET | Admin | Audit logs |
| `/hangfire` | GET | Admin | Background jobs dashboard |

## Send Email

Send a transactional email.

**POST** `/emails`

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
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "object": "email"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | GUID that uniquely identifies the sent email in SelfMX |
| `object` | string | Always `"email"` |

This format is compatible with official Resend SDKs.

### Example

```bash
curl -X POST https://mail.yourdomain.com/emails \
  -H "Authorization: Bearer re_xxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d '{
    "from": "hello@yourdomain.com",
    "to": "recipient@example.com",
    "subject": "Hello from SelfMX",
    "html": "<h1>Welcome!</h1><p>Your first email from SelfMX.</p>"
  }'
```

## Get Email

Retrieve a previously sent email by ID. Returns Resend-compatible fields.

**GET** `/emails/{id}`

### Response

```json
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "from": "hello@yourdomain.com",
  "to": ["recipient@example.com"],
  "cc": ["cc@example.com"],
  "bcc": ["bcc@example.com"],
  "reply_to": ["reply@yourdomain.com"],
  "subject": "Hello from SelfMX",
  "html": "<p>HTML content</p>",
  "text": "Plain text content",
  "created_at": "2024-01-15T10:30:00Z",
  "last_event": null
}
```

### Notes

- Returns `404` if the email ID does not exist
- Returns `403` if the API key does not have access to the domain used to send the email
- Fields `cc`, `bcc`, `reply_to`, `text`, `html`, `scheduled_at`, and `last_event` are omitted from the response when null

## List Emails

List sent emails with cursor-based pagination.

**GET** `/emails`

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `before` | string | - | Cursor: email ID to paginate before |
| `after` | string | - | Cursor: email ID to paginate after |
| `limit` | integer | 20 | Items per page (1-100) |

### Response

```json
{
  "data": [
    {
      "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "from": "hello@yourdomain.com",
      "to": ["recipient@example.com"],
      "subject": "Hello from SelfMX",
      "created_at": "2024-01-15T10:30:00Z",
      "last_event": null
    }
  ],
  "has_more": true
}
```

### Notes

- Non-admin API keys only see emails sent from their authorized domains
- Admin keys see all sent emails

## Send Batch Emails

Send multiple emails in a single request.

**POST** `/emails/batch`

### Request

An array of email objects (same schema as Send Email):

```json
[
  {
    "from": "hello@yourdomain.com",
    "to": ["alice@example.com"],
    "subject": "Hello Alice",
    "html": "<p>Hi Alice</p>"
  },
  {
    "from": "hello@yourdomain.com",
    "to": ["bob@example.com"],
    "subject": "Hello Bob",
    "html": "<p>Hi Bob</p>"
  }
]
```

### Headers

| Header | Values | Default | Description |
|--------|--------|---------|-------------|
| `x-batch-validation` | `strict`, `permissive` | `strict` | Validation mode |

- **strict** (default): All emails are validated before any are sent. If any email fails validation, no emails are sent.
- **permissive**: Emails are sent individually. Failed emails are reported in the `errors` array but don't prevent other emails from being sent.

### Response (strict mode)

```json
{
  "data": [
    { "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" },
    { "id": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy" }
  ]
}
```

### Response (permissive mode)

```json
{
  "data": [
    { "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" }
  ],
  "errors": [
    { "index": 1, "message": "Domain not verified: other.com" }
  ]
}
```

### Notes

- In strict mode, validation errors return `400` or `422` with a single error response
- In permissive mode, the response always returns `200` with both `data` and optional `errors` arrays

## List Domains

Get all domains for the authenticated API key.

**GET** `/domains`

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

**POST** `/domains`

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
curl -X POST https://mail.yourdomain.com/domains \
  -H "Authorization: Bearer re_xxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d '{"name": "example.com"}'
```

## Get Domain

Get details for a specific domain including DNS records.

**GET** `/domains/{id}`

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

**DELETE** `/domains/{id}`

### Response

```
204 No Content
```

## Verify Domain

Manually trigger a verification check for a domain. Only works for domains in `Verifying` status.

**POST** `/domains/{id}/verify`

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
curl -X POST https://mail.yourdomain.com/domains/{id}/verify \
  -H "Authorization: Bearer re_xxxxxxxxxxxx"
```

### Notes

- Returns `400 Bad Request` if the domain is not in `Verifying` status
- Use this to immediately check verification instead of waiting for the next scheduled check (every 5 minutes)
- After verification succeeds, `status` becomes `verified` and `nextCheckAt` becomes `null`

## Send Test Email

Send a test email from a verified domain. Useful for verifying domain configuration.

**POST** `/domains/{id}/test-email`

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
curl -X POST https://mail.yourdomain.com/domains/{id}/test-email \
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

## Token Introspection

Get effective permissions for the current authentication token. Useful for verifying token validity and discovering which domains an API key can access.

**GET** `/tokens/me`

### Response

```json
{
  "authenticated": true,
  "actorType": "api_key",
  "isAdmin": false,
  "name": "Production",
  "keyId": "k5f2a3b1-...",
  "keyPrefix": "re_abc123",
  "allowedDomainIds": ["d5f2a3b1-...", "d5f2a3b2-..."]
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `authenticated` | boolean | Always `true` for successful requests |
| `actorType` | string | `"admin"` for admin sessions/keys, `"api_key"` for regular keys |
| `isAdmin` | boolean | Whether the caller has admin privileges |
| `name` | string | Identity name (null for some auth types) |
| `keyId` | string | API key ID (present for API key auth) |
| `keyPrefix` | string | API key prefix (present for API key auth) |
| `allowedDomainIds` | array | Domain IDs this key can access (empty for admin) |

### Example

```bash
curl https://mail.yourdomain.com/tokens/me \
  -H "Authorization: Bearer re_xxxxxxxxxxxx"
```

### Notes

- Returns `401` if the token is invalid or missing
- Admin sessions return `actorType: "admin"` with an empty `allowedDomainIds` array
- `keyId` and `keyPrefix` are `null` when authenticated via cookie session

## List API Keys (Admin)

Get all API keys. Requires admin authentication.

**GET** `/api-keys`

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

**POST** `/api-keys`

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

**DELETE** `/api-keys/{id}`

### Response

```
204 No Content
```

## List Archived API Keys (Admin)

Get archived (previously revoked) API keys. Revoked API keys are automatically archived after 90 days by a daily cleanup job. Requires admin authentication.

**GET** `/api-keys/revoked`

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

**GET** `/sent-emails`

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
curl https://mail.yourdomain.com/sent-emails?pageSize=50

# Next page (use nextCursor from previous response)
curl https://mail.yourdomain.com/sent-emails?cursor=eyJpZCI6IjEyMyJ9
```

## Get Sent Email (Admin)

Get details of a specific sent email including the full body. Requires admin authentication.

**GET** `/sent-emails/{id}`

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

**GET** `/audit`

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

All errors return a Resend-compatible JSON format:

```json
{
  "statusCode": 422,
  "name": "validation_error",
  "message": "Domain not verified: example.com",
  "error": {
    "code": "domain_not_verified",
    "message": "Domain not verified: example.com"
  }
}
```

### Error Names

| Name | Description |
|------|-------------|
| `missing_api_key` | No API key provided |
| `invalid_api_key` | API key is invalid or revoked |
| `invalid_access` | Key doesn't have access to the requested resource |
| `validation_error` | Request validation failed (missing fields, unverified domain) |
| `not_found` | Resource doesn't exist |
| `missing_required_field` | Required request fields are missing |
| `rate_limit_exceeded` | Too many requests |
| `internal_server_error` | Server error |

### HTTP Status Codes

| Code | Description |
|------|-------------|
| `400` | Bad Request - Invalid input or missing fields |
| `401` | Unauthorized - Invalid or missing API key |
| `403` | Forbidden - Key doesn't have access to domain |
| `404` | Not Found - Resource doesn't exist |
| `422` | Unprocessable Entity - Domain not verified |
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

**GET** `/system/status`

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

**GET** `/system/version`

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

**GET** `/system/logs`

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
curl "https://mail.yourdomain.com/system/logs?count=100&level=Error" \
  -H "Cookie: auth_session=your_session_cookie"

# Get logs from a specific category
curl "https://mail.yourdomain.com/system/logs?category=SesService" \
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
