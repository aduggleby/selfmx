---
layout: ../../layouts/DocsLayout.astro
title: Email Processing
description: How SelfMX processes and delivers emails.
---

Understanding the email processing pipeline helps you optimize delivery and handle failures.

## Processing Pipeline

```
┌─────────┐   ┌──────────┐   ┌──────────┐   ┌─────────┐   ┌───────────┐
│ API     │──▶│  Queue   │──▶│  Render  │──▶│  Send   │──▶│  Webhook  │
│ Request │   │ (queued) │   │ Template │   │  SMTP   │   │  Notify   │
└─────────┘   └──────────┘   └──────────┘   └─────────┘   └───────────┘
```

## Email States

| State      | Description                              |
| ---------- | ---------------------------------------- |
| `queued`   | Email saved, waiting for processing      |
| `sending`  | Worker picked up, attempting delivery    |
| `delivered`| SMTP confirmed delivery                  |
| `bounced`  | Recipient server rejected                |
| `failed`   | Permanent failure after retries          |

### State Transitions

```
queued → sending → delivered
                 → bounced
                 → failed (after retries)
```

## Queue Processing

### Worker Behavior

Workers continuously:

1. **Poll** - Check for `queued` emails
2. **Claim** - Mark email as `sending` (prevents duplicates)
3. **Process** - Render template, send via SMTP
4. **Update** - Set final status (`delivered`, `bounced`, `failed`)
5. **Notify** - Send webhook if configured

### Concurrency

Multiple workers process emails in parallel:

```bash
QUEUE_WORKERS=8  # 8 concurrent workers
```

Each worker processes one email at a time. More workers = higher throughput.

### Ordering

Emails are processed in FIFO order (first in, first out) within the same priority level.

## Template Rendering

When using templates, variables are substituted at send time:

```json
{
  "template_id": "welcome",
  "template_data": {
    "name": "John",
    "company": "Acme"
  }
}
```

Template:

```html
<h1>Welcome, {{name}}!</h1>
<p>Thanks for joining {{company}}.</p>
```

Rendered:

```html
<h1>Welcome, John!</h1>
<p>Thanks for joining Acme.</p>
```

### Template Syntax

SelfMX uses Handlebars-style syntax:

- `{{variable}}` - Simple substitution
- `{{#if condition}}...{{/if}}` - Conditionals
- `{{#each items}}...{{/each}}` - Loops

## Retry Logic

Failed emails are retried automatically:

```bash
QUEUE_RETRY_ATTEMPTS=3   # Max attempts
QUEUE_RETRY_DELAY=60     # Initial delay (seconds)
```

### Exponential Backoff

Retry delays increase exponentially:

- Attempt 1: Immediate
- Attempt 2: 1 minute
- Attempt 3: 5 minutes
- Attempt 4: 30 minutes (if configured)

### Retryable Errors

Only transient errors trigger retries:

| Retryable              | Not Retryable           |
| ---------------------- | ----------------------- |
| Connection timeout     | Invalid recipient       |
| SMTP 421 (try later)   | SMTP 550 (user unknown) |
| DNS resolution failure | Authentication failed   |
| TLS handshake error    | Invalid API key         |

## Bounce Handling

### Hard Bounces

Permanent delivery failures:

- Invalid email address
- Domain doesn't exist
- Mailbox doesn't exist

Hard bounces are **not retried** and trigger `bounced` webhooks.

### Soft Bounces

Temporary delivery failures:

- Mailbox full
- Server temporarily unavailable
- Rate limited

Soft bounces are **retried** according to retry policy.

## Rate Limiting

### SMTP Rate Limits

Many SMTP providers have sending limits:

| Provider   | Limit                    |
| ---------- | ------------------------ |
| SendGrid   | Varies by plan           |
| Mailgun    | 300/minute (free tier)   |
| Amazon SES | 14/second (default)      |

SelfMX respects rate limits via connection pooling:

```bash
SMTP_POOL_SIZE=10  # Max concurrent connections
```

### API Rate Limits

Protect your API with rate limiting:

```bash
RATE_LIMIT_REQUESTS=100  # Per minute
RATE_LIMIT_WINDOW=60     # Window in seconds
```

## Logging

All email activity is logged:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "email_id": "msg_abc123",
  "event": "delivered",
  "to": "recipient@example.com",
  "smtp_response": "250 OK"
}
```

Query logs via API:

```bash
curl https://api.yourdomain.com/v1/logs?email_id=msg_abc123 \
  -H "Authorization: Bearer YOUR_API_KEY"
```

## Performance Optimization

### Batch Sending

For high volume, consider:

- Increase `QUEUE_WORKERS`
- Use faster SMTP relay
- Optimize PostgreSQL (indexes, connection pool)

### Monitoring Queue Depth

Alert when queue grows too large:

```sql
SELECT COUNT(*) FROM emails WHERE status = 'queued';
```

Healthy queue depth should stay low (< 100 pending).

## Next Steps

- [Multi-Tenancy](/concepts/tenants) - Isolated email sending
- [Monitoring](/guides/monitoring) - Track email metrics
