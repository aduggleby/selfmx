---
layout: ../../layouts/DocsLayout.astro
title: Webhooks
description: Receive real-time notifications for email events.
---

Webhooks notify your application when email events occur, such as deliveries, bounces, or complaints.

## Setting Up Webhooks

### Create a Webhook

```bash
curl -X POST https://api.yourdomain.com/v1/webhooks \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://yourapp.com/webhooks/email",
    "events": ["delivered", "bounced", "complained"],
    "secret": "your-webhook-secret"
  }'
```

Response:

```json
{
  "id": "wh_abc123",
  "url": "https://yourapp.com/webhooks/email",
  "events": ["delivered", "bounced", "complained"],
  "active": true,
  "created_at": "2024-01-15T10:30:00Z"
}
```

## Event Types

| Event       | Description                              |
| ----------- | ---------------------------------------- |
| `queued`    | Email added to queue                     |
| `sending`   | Email being sent                         |
| `delivered` | Email successfully delivered             |
| `bounced`   | Email bounced (hard or soft)             |
| `complained`| Recipient marked as spam                 |
| `opened`    | Email was opened (if tracking enabled)   |
| `clicked`   | Link was clicked (if tracking enabled)   |

## Webhook Payload

All webhooks send a POST request with JSON body:

```json
{
  "id": "evt_xyz789",
  "type": "delivered",
  "created_at": "2024-01-15T10:30:05Z",
  "data": {
    "email_id": "msg_abc123",
    "from": "sender@yourdomain.com",
    "to": "recipient@example.com",
    "subject": "Hello",
    "delivered_at": "2024-01-15T10:30:05Z"
  }
}
```

### Bounce Payload

```json
{
  "id": "evt_xyz789",
  "type": "bounced",
  "created_at": "2024-01-15T10:30:05Z",
  "data": {
    "email_id": "msg_abc123",
    "from": "sender@yourdomain.com",
    "to": "invalid@example.com",
    "bounce_type": "hard",
    "bounce_code": "550",
    "bounce_message": "User unknown"
  }
}
```

## Verifying Webhooks

Verify webhook authenticity using the signature header:

```
X-Webhook-Signature: sha256=abc123...
```

### Verification Example (Node.js)

```javascript
const crypto = require('crypto');

function verifyWebhook(payload, signature, secret) {
  const expected = crypto
    .createHmac('sha256', secret)
    .update(payload)
    .digest('hex');

  return `sha256=${expected}` === signature;
}

app.post('/webhooks/email', (req, res) => {
  const signature = req.headers['x-webhook-signature'];
  const payload = JSON.stringify(req.body);

  if (!verifyWebhook(payload, signature, process.env.WEBHOOK_SECRET)) {
    return res.status(401).json({ error: 'Invalid signature' });
  }

  // Process webhook
  console.log('Event:', req.body.type);
  res.status(200).json({ received: true });
});
```

### Verification Example (Python)

```python
import hmac
import hashlib

def verify_webhook(payload, signature, secret):
    expected = 'sha256=' + hmac.new(
        secret.encode(),
        payload.encode(),
        hashlib.sha256
    ).hexdigest()
    return hmac.compare_digest(expected, signature)

@app.route('/webhooks/email', methods=['POST'])
def handle_webhook():
    signature = request.headers.get('X-Webhook-Signature')
    payload = request.get_data(as_text=True)

    if not verify_webhook(payload, signature, WEBHOOK_SECRET):
        return jsonify({'error': 'Invalid signature'}), 401

    event = request.json
    print(f"Event: {event['type']}")
    return jsonify({'received': True})
```

## Managing Webhooks

### List Webhooks

```bash
curl https://api.yourdomain.com/v1/webhooks \
  -H "Authorization: Bearer YOUR_API_KEY"
```

### Update Webhook

```bash
curl -X PUT https://api.yourdomain.com/v1/webhooks/wh_abc123 \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "events": ["delivered", "bounced", "complained", "opened"]
  }'
```

### Delete Webhook

```bash
curl -X DELETE https://api.yourdomain.com/v1/webhooks/wh_abc123 \
  -H "Authorization: Bearer YOUR_API_KEY"
```

### Webhook Logs

View recent webhook deliveries:

```bash
curl https://api.yourdomain.com/v1/webhooks/wh_abc123/logs \
  -H "Authorization: Bearer YOUR_API_KEY"
```

## Retry Policy

Failed webhook deliveries are retried:

- **3 attempts** with exponential backoff
- Retry intervals: 1 minute, 5 minutes, 30 minutes
- After all retries fail, the webhook is marked as failed

## Best Practices

1. **Return 200 quickly** - Process webhooks asynchronously
2. **Verify signatures** - Always validate webhook authenticity
3. **Handle duplicates** - Webhooks may be delivered more than once
4. **Use HTTPS** - Always use secure endpoints
5. **Monitor failures** - Set up alerts for webhook delivery failures

## Next Steps

- [Monitoring](/guides/monitoring) - Set up alerts for email events
- [Troubleshooting](/guides/troubleshooting) - Debug webhook issues
