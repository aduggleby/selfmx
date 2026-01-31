---
layout: ../../layouts/DocsLayout.astro
title: Quick Start
description: Send your first email with SelfMX in minutes.
---

This guide will walk you through sending your first email with SelfMX.

## Prerequisites

Make sure you have SelfMX [installed and running](/getting-started/installation).

## Send Your First Email

### Using cURL

```bash
curl -X POST http://localhost:8080/v1/send \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "from": "sender@yourdomain.com",
    "to": "recipient@example.com",
    "subject": "Hello from SelfMX",
    "html": "<h1>Welcome!</h1><p>Your first email from SelfMX.</p>"
  }'
```

### Using JavaScript

```javascript
const response = await fetch("http://localhost:8080/v1/send", {
  method: "POST",
  headers: {
    Authorization: "Bearer YOUR_API_KEY",
    "Content-Type": "application/json",
  },
  body: JSON.stringify({
    from: "sender@yourdomain.com",
    to: "recipient@example.com",
    subject: "Hello from SelfMX",
    html: "<h1>Welcome!</h1><p>Your first email from SelfMX.</p>",
  }),
});

const result = await response.json();
console.log(result);
```

### Using Python

```python
import requests

response = requests.post(
    "http://localhost:8080/v1/send",
    headers={
        "Authorization": "Bearer YOUR_API_KEY",
        "Content-Type": "application/json"
    },
    json={
        "from": "sender@yourdomain.com",
        "to": "recipient@example.com",
        "subject": "Hello from SelfMX",
        "html": "<h1>Welcome!</h1><p>Your first email from SelfMX.</p>"
    }
)

print(response.json())
```

### Using C#

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("Authorization", "Bearer YOUR_API_KEY");

var content = new StringContent(
    JsonSerializer.Serialize(new {
        from = "sender@yourdomain.com",
        to = "recipient@example.com",
        subject = "Hello from SelfMX",
        html = "<h1>Welcome!</h1><p>Your first email from SelfMX.</p>"
    }),
    Encoding.UTF8,
    "application/json"
);

var response = await client.PostAsync("http://localhost:8080/v1/send", content);
var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);
```

## Response

A successful request returns:

```json
{
  "id": "msg_abc123",
  "status": "queued",
  "created_at": "2024-01-15T10:30:00Z"
}
```

## Next Steps

- [Basic Usage](/getting-started/basic-usage) - Learn more API features
- [API Reference](/api) - Complete endpoint documentation
- [Templates](/api/endpoints#templates) - Use dynamic email templates
