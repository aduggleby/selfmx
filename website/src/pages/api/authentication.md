---
layout: ../../layouts/DocsLayout.astro
title: Authentication
description: API authentication and key management.
---

SelfMX uses API keys for authentication. Each request must include a valid API key.

## Using API Keys

Include the API key in the Authorization header:

```bash
curl -H "Authorization: Bearer YOUR_API_KEY" \
  https://api.yourdomain.com/v1/send
```

## Creating API Keys

### Default Key

The initial API key is set via environment variable:

```bash
API_KEY=your-default-api-key
```

### Additional Keys

Create additional API keys via the API:

```bash
curl -X POST https://api.yourdomain.com/v1/api-keys \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Production App",
    "permissions": ["send", "templates:read"]
  }'
```

Response:

```json
{
  "id": "key_abc123",
  "name": "Production App",
  "key": "sk_live_xxxxxxxxxxxx",
  "permissions": ["send", "templates:read"],
  "created_at": "2024-01-15T10:30:00Z"
}
```

**Important:** The full key is only shown once. Store it securely.

## Key Permissions

| Permission        | Description                    |
| ----------------- | ------------------------------ |
| `send`            | Send emails                    |
| `templates:read`  | View templates                 |
| `templates:write` | Create/update templates        |
| `webhooks:read`   | View webhook configurations    |
| `webhooks:write`  | Configure webhooks             |
| `api-keys:read`   | List API keys                  |
| `api-keys:write`  | Create/revoke API keys         |
| `logs:read`       | View email logs                |
| `*`               | Full access (admin)            |

## Listing API Keys

```bash
curl https://api.yourdomain.com/v1/api-keys \
  -H "Authorization: Bearer YOUR_API_KEY"
```

Response:

```json
{
  "data": [
    {
      "id": "key_abc123",
      "name": "Production App",
      "permissions": ["send", "templates:read"],
      "last_used_at": "2024-01-15T10:30:00Z",
      "created_at": "2024-01-10T08:00:00Z"
    }
  ]
}
```

## Revoking API Keys

```bash
curl -X DELETE https://api.yourdomain.com/v1/api-keys/key_abc123 \
  -H "Authorization: Bearer YOUR_API_KEY"
```

Response:

```json
{
  "id": "key_abc123",
  "revoked": true
}
```

## Multi-Tenant Keys

For multi-tenant setups, API keys can be scoped to specific tenants:

```bash
curl -X POST https://api.yourdomain.com/v1/api-keys \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Tenant A - Production",
    "tenant_id": "tenant_abc",
    "permissions": ["send"]
  }'
```

See [Multi-Tenancy](/concepts/tenants) for more details.

## Security Best Practices

1. **Never expose keys in client-side code** - Use server-side API calls
2. **Use environment variables** - Don't hardcode keys in source code
3. **Rotate keys regularly** - Revoke and create new keys periodically
4. **Limit permissions** - Only grant necessary permissions
5. **Monitor usage** - Review API key activity in logs

## Next Steps

- [Endpoints](/api/endpoints) - API endpoint reference
- [Multi-Tenancy](/concepts/tenants) - Tenant-scoped API keys
