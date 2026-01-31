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
| `/v1/emails` | POST | API Key | Send email |
| `/v1/domains` | GET | API Key | List domains |
| `/v1/domains` | POST | API Key | Create domain |
| `/v1/domains/{id}` | GET | API Key | Get domain |
| `/v1/domains/{id}` | DELETE | API Key | Delete domain |
| `/v1/api-keys` | GET | Admin | List API keys |
| `/v1/api-keys` | POST | Admin | Create API key |
| `/v1/audit` | GET | Admin | Audit logs |

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
      "status": "Verified",
      "createdAt": "2024-01-15T10:30:00Z"
    }
  ]
}
```

### Domain Status Values

| Status | Description |
|--------|-------------|
| `Pending` | Domain added, waiting for setup job |
| `Verifying` | DNS records created, waiting for verification |
| `Verified` | Domain verified and ready for sending |
| `Failed` | Verification failed |

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
  "status": "Pending",
  "createdAt": "2024-01-15T10:30:00Z"
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
  "status": "Verifying",
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
  "createdAt": "2024-01-15T10:30:00Z"
}
```

## Delete Domain

Remove a domain from SelfMX and AWS SES.

**DELETE** `/v1/domains/{id}`

### Response

```
204 No Content
```

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
