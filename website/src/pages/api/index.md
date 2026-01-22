---
layout: ../../layouts/DocsLayout.astro
title: API Reference
description: Complete REST API documentation for SelfMX.
---

SelfMX provides a REST API for sending emails, managing templates, and configuring webhooks.

## Base URL

```
https://api.yourdomain.com/v1
```

All API endpoints are prefixed with `/v1`.

## Authentication

All requests require authentication via Bearer token:

```bash
curl -H "Authorization: Bearer YOUR_API_KEY" \
  https://api.yourdomain.com/v1/send
```

See [Authentication](/api/authentication) for details.

## Response Format

All responses are JSON:

```json
{
  "id": "msg_abc123",
  "status": "queued",
  "created_at": "2024-01-15T10:30:00Z"
}
```

## Error Handling

Errors return appropriate HTTP status codes:

```json
{
  "error": {
    "code": "invalid_request",
    "message": "The 'to' field is required"
  }
}
```

| Status | Description              |
| ------ | ------------------------ |
| 400    | Bad Request              |
| 401    | Unauthorized             |
| 403    | Forbidden                |
| 404    | Not Found                |
| 429    | Rate Limited             |
| 500    | Internal Server Error    |

## Rate Limiting

Default rate limits:

- **100 requests** per minute per API key
- Rate limit headers included in responses:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1705312260
```

## API Sections

- [Authentication](/api/authentication) - API key management and security
- [Endpoints](/api/endpoints) - Send emails, manage templates
- [Webhooks](/api/webhooks) - Delivery notifications and events
