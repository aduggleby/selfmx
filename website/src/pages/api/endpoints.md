---
layout: ../../layouts/DocsLayout.astro
title: Endpoints
description: Complete API endpoint reference.
---

Reference documentation for all SelfMX API endpoints.

## Send Email

Send a single email.

```http
POST /v1/send
```

### Request Body

```json
{
  "from": "sender@yourdomain.com",
  "to": "recipient@example.com",
  "subject": "Hello",
  "html": "<p>Email content</p>",
  "text": "Email content"
}
```

### Parameters

| Field         | Type            | Required | Description                    |
| ------------- | --------------- | -------- | ------------------------------ |
| `from`        | string          | Yes      | Sender email address           |
| `to`          | string/array    | Yes      | Recipient(s)                   |
| `subject`     | string          | Yes      | Email subject                  |
| `html`        | string          | No*      | HTML body                      |
| `text`        | string          | No*      | Plain text body                |
| `cc`          | string/array    | No       | CC recipients                  |
| `bcc`         | string/array    | No       | BCC recipients                 |
| `reply_to`    | string          | No       | Reply-to address               |
| `headers`     | object          | No       | Custom headers                 |
| `attachments` | array           | No       | File attachments               |
| `template_id` | string          | No       | Template ID                    |
| `template_data` | object        | No       | Template variables             |
| `tags`        | array           | No       | Tracking tags                  |
| `metadata`    | object          | No       | Custom metadata                |

*At least one of `html` or `text` is required unless using a template.

### Response

```json
{
  "id": "msg_abc123",
  "status": "queued",
  "created_at": "2024-01-15T10:30:00Z"
}
```

---

## List Sent Emails

List sent emails with keyset pagination.

```http
GET /v1/sent-emails
```

### Query Parameters

| Parameter  | Type     | Default | Description                    |
| ---------- | -------- | ------- | ------------------------------ |
| `domainId` | string   | -       | Filter by domain ID            |
| `from`     | datetime | -       | Filter emails sent after       |
| `to`       | datetime | -       | Filter emails sent before      |
| `cursor`   | string   | -       | Pagination cursor              |
| `pageSize` | int      | 50      | Items per page (max 100)       |

### Response

```json
{
  "data": [
    {
      "id": "abc123-def456",
      "messageId": "0100018abc123-def456-789@email.amazonses.com",
      "sentAt": "2024-01-15T10:30:00Z",
      "fromAddress": "sender@yourdomain.com",
      "to": ["recipient@example.com"],
      "subject": "Hello",
      "domainId": "domain-id-123"
    }
  ],
  "nextCursor": "eyJJZCI6ImFiYzEyMyIsIlNlbnRBdCI6Ii4uLiJ9",
  "hasMore": true
}
```

The list view excludes email body content for efficiency. Use the detail endpoint to retrieve full email content.

---

## Get Sent Email

Retrieve full sent email details including body content.

```http
GET /v1/sent-emails/{id}
```

### Response

```json
{
  "id": "abc123-def456",
  "messageId": "0100018abc123-def456-789@email.amazonses.com",
  "sentAt": "2024-01-15T10:30:00Z",
  "fromAddress": "sender@yourdomain.com",
  "to": ["recipient@example.com"],
  "cc": ["cc@example.com"],
  "replyTo": "reply@yourdomain.com",
  "subject": "Hello",
  "htmlBody": "<p>Email content</p>",
  "textBody": "Email content",
  "domainId": "domain-id-123"
}
```

> **Note**: BCC recipients are never returned in API responses for privacy.

---

## Templates

### Create Template

```http
POST /v1/templates
```

```json
{
  "name": "welcome-email",
  "subject": "Welcome, {{name}}!",
  "html": "<h1>Welcome, {{name}}!</h1><p>Thanks for joining {{company}}.</p>",
  "text": "Welcome, {{name}}! Thanks for joining {{company}}."
}
```

### Get Template

```http
GET /v1/templates/{id}
```

### List Templates

```http
GET /v1/templates
```

### Update Template

```http
PUT /v1/templates/{id}
```

### Delete Template

```http
DELETE /v1/templates/{id}
```

---

## Health Check

```http
GET /health
```

No authentication required.

### Response

```json
{
  "status": "ok",
  "version": "1.0.0",
  "database": "connected",
  "smtp": "connected"
}
```

---

## Test SMTP

Test SMTP configuration.

```http
POST /v1/test-smtp
```

### Response

```json
{
  "status": "ok",
  "message": "SMTP connection successful"
}
```

## Next Steps

- [Webhooks](/api/webhooks) - Delivery event notifications
- [Basic Usage](/getting-started/basic-usage) - Common patterns
