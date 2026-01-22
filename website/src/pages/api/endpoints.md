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

## Get Email

Retrieve email details by ID.

```http
GET /v1/emails/{id}
```

### Response

```json
{
  "id": "msg_abc123",
  "status": "delivered",
  "from": "sender@yourdomain.com",
  "to": ["recipient@example.com"],
  "subject": "Hello",
  "created_at": "2024-01-15T10:30:00Z",
  "delivered_at": "2024-01-15T10:30:05Z"
}
```

### Status Values

| Status      | Description                    |
| ----------- | ------------------------------ |
| `queued`    | Email is queued for sending    |
| `sending`   | Email is being sent            |
| `delivered` | Email was delivered            |
| `bounced`   | Email bounced                  |
| `failed`    | Email failed to send           |

---

## List Emails

List sent emails with pagination.

```http
GET /v1/emails
```

### Query Parameters

| Parameter | Type   | Default | Description                    |
| --------- | ------ | ------- | ------------------------------ |
| `page`    | int    | 1       | Page number                    |
| `limit`   | int    | 20      | Items per page (max 100)       |
| `status`  | string | -       | Filter by status               |
| `from`    | string | -       | Filter by sender               |
| `to`      | string | -       | Filter by recipient            |

### Response

```json
{
  "data": [
    {
      "id": "msg_abc123",
      "status": "delivered",
      "from": "sender@yourdomain.com",
      "to": ["recipient@example.com"],
      "subject": "Hello",
      "created_at": "2024-01-15T10:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 150
  }
}
```

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
