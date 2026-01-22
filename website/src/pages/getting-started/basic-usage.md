---
layout: ../../layouts/DocsLayout.astro
title: Basic Usage
description: Learn the core features and common patterns for using SelfMX.
---

This guide covers common use cases and patterns for sending emails with SelfMX.

## Email Fields

Every email requires these fields:

| Field     | Type   | Required | Description                    |
| --------- | ------ | -------- | ------------------------------ |
| `from`    | string | Yes      | Sender email address           |
| `to`      | string | Yes      | Recipient email address        |
| `subject` | string | Yes      | Email subject line             |
| `html`    | string | No\*     | HTML body content              |
| `text`    | string | No\*     | Plain text body content        |

\*At least one of `html` or `text` is required.

## Multiple Recipients

Send to multiple recipients:

```json
{
  "from": "sender@yourdomain.com",
  "to": ["user1@example.com", "user2@example.com"],
  "subject": "Team Update",
  "html": "<p>Important announcement...</p>"
}
```

## CC and BCC

Include CC and BCC recipients:

```json
{
  "from": "sender@yourdomain.com",
  "to": "primary@example.com",
  "cc": ["manager@example.com"],
  "bcc": ["archive@example.com"],
  "subject": "Project Update",
  "html": "<p>Status report...</p>"
}
```

## Reply-To Address

Set a different reply-to address:

```json
{
  "from": "noreply@yourdomain.com",
  "to": "customer@example.com",
  "reply_to": "support@yourdomain.com",
  "subject": "Your Order Confirmation",
  "html": "<p>Thank you for your order...</p>"
}
```

## Custom Headers

Add custom email headers:

```json
{
  "from": "sender@yourdomain.com",
  "to": "recipient@example.com",
  "subject": "Newsletter",
  "html": "<p>This week's news...</p>",
  "headers": {
    "X-Campaign-ID": "newsletter-2024-01",
    "List-Unsubscribe": "<mailto:unsubscribe@yourdomain.com>"
  }
}
```

## Attachments

Include file attachments:

```json
{
  "from": "sender@yourdomain.com",
  "to": "recipient@example.com",
  "subject": "Document Attached",
  "html": "<p>Please find the document attached.</p>",
  "attachments": [
    {
      "filename": "report.pdf",
      "content": "base64-encoded-content",
      "content_type": "application/pdf"
    }
  ]
}
```

## Using Templates

Reference a pre-defined template:

```json
{
  "from": "sender@yourdomain.com",
  "to": "customer@example.com",
  "template_id": "welcome-email",
  "template_data": {
    "name": "John",
    "company": "Acme Inc"
  }
}
```

## Checking Email Status

Query the status of a sent email:

```bash
curl http://localhost:8080/v1/emails/msg_abc123 \
  -H "Authorization: Bearer YOUR_API_KEY"
```

Response:

```json
{
  "id": "msg_abc123",
  "status": "delivered",
  "from": "sender@yourdomain.com",
  "to": "recipient@example.com",
  "subject": "Hello",
  "created_at": "2024-01-15T10:30:00Z",
  "delivered_at": "2024-01-15T10:30:05Z"
}
```

## Next Steps

- [API Reference](/api) - Complete endpoint documentation
- [Templates](/api/endpoints#templates) - Create and manage templates
- [Webhooks](/api/webhooks) - Set up delivery notifications
