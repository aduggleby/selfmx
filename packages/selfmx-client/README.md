# selfmx-client

TypeScript client for Selfmx email API. Resend-compatible interface.

## Installation

```bash
npm install selfmx-client
```

## Quick Start

```typescript
import { SelfmxClient } from 'selfmx-client';

const client = new SelfmxClient({
  apiKey: 'your-api-key',
  baseUrl: 'https://your-selfmx-instance.com',
});

// Send an email
const { id } = await client.emails.send({
  from: 'hello@example.com',
  to: 'user@example.com',
  subject: 'Hello World',
  html: '<p>Hello from Selfmx!</p>',
});

console.log('Email sent:', id);
```

## Usage

### Send Email

```typescript
await client.emails.send({
  from: 'hello@example.com',
  to: ['user1@example.com', 'user2@example.com'],
  subject: 'Hello',
  html: '<p>HTML content</p>',
  text: 'Plain text content',
  cc: 'cc@example.com',
  bcc: 'bcc@example.com',
  replyTo: 'reply@example.com',
});
```

### Manage Domains

```typescript
// List domains
const { data, total } = await client.domains.list();

// Create domain
const domain = await client.domains.create({ name: 'example.com' });

// Get domain
const domain = await client.domains.get('domain-id');

// Delete domain
await client.domains.delete('domain-id');
```

## Error Handling

```typescript
import { SelfmxClient, SelfmxError } from 'selfmx-client';

try {
  await client.emails.send({ ... });
} catch (error) {
  if (error instanceof SelfmxError) {
    console.error('API Error:', error.code, error.message);
  }
}
```

## License

MIT
