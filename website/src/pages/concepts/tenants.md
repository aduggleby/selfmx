---
layout: ../../layouts/DocsLayout.astro
title: Multi-Tenancy
description: Manage multiple domains and API keys with tenant isolation.
---

SelfMX supports multi-tenant configurations for managing multiple domains, API keys, and isolated settings.

## Overview

Multi-tenancy allows you to:

- **Isolate domains** - Separate sending domains
- **Manage API keys** - Tenant-scoped authentication
- **Track usage** - Per-tenant metrics
- **Configure separately** - Different SMTP settings per tenant

## Tenant Model

```
┌─────────────────────────────────────────────────────────────────┐
│                          SelfMX                                 │
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │    Tenant A     │  │    Tenant B     │  │    Tenant C     │ │
│  │  app.com        │  │  service.io     │  │  startup.co     │ │
│  │                 │  │                 │  │                 │ │
│  │  API Keys: 2    │  │  API Keys: 1    │  │  API Keys: 3    │ │
│  │  Templates: 5   │  │  Templates: 2   │  │  Templates: 8   │ │
│  │  Webhooks: 1    │  │  Webhooks: 0    │  │  Webhooks: 2    │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Creating Tenants

### Create a Tenant

```bash
curl -X POST https://api.yourdomain.com/v1/tenants \
  -H "Authorization: Bearer ADMIN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Acme Corp",
    "domain": "acme.com",
    "settings": {
      "from_name": "Acme",
      "reply_to": "support@acme.com"
    }
  }'
```

Response:

```json
{
  "id": "tenant_abc123",
  "name": "Acme Corp",
  "domain": "acme.com",
  "settings": {
    "from_name": "Acme",
    "reply_to": "support@acme.com"
  },
  "created_at": "2024-01-15T10:30:00Z"
}
```

### Create Tenant API Key

```bash
curl -X POST https://api.yourdomain.com/v1/tenants/tenant_abc123/api-keys \
  -H "Authorization: Bearer ADMIN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Production",
    "permissions": ["send", "templates:read"]
  }'
```

## Tenant Isolation

Each tenant has isolated:

### Data

- Emails sent only visible to tenant
- Templates scoped to tenant
- Webhooks configured per tenant

### API Keys

- API keys belong to one tenant
- Keys can only access their tenant's data

### Settings

- SMTP configuration (optional override)
- Default from address
- Rate limits

## Tenant-Scoped API Requests

When using a tenant API key, all requests are automatically scoped:

```bash
# Using tenant API key - only sees Acme's emails
curl https://api.yourdomain.com/v1/emails \
  -H "Authorization: Bearer TENANT_API_KEY"
```

## Admin Access

Admin API keys can access all tenants:

```bash
# List all tenants
curl https://api.yourdomain.com/v1/tenants \
  -H "Authorization: Bearer ADMIN_API_KEY"
```

```bash
# Access specific tenant data
curl https://api.yourdomain.com/v1/tenants/tenant_abc123/emails \
  -H "Authorization: Bearer ADMIN_API_KEY"
```

## Per-Tenant SMTP

Override SMTP settings per tenant:

```bash
curl -X PUT https://api.yourdomain.com/v1/tenants/tenant_abc123 \
  -H "Authorization: Bearer ADMIN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "smtp": {
      "host": "smtp.acme.com",
      "port": 587,
      "user": "noreply@acme.com",
      "pass": "tenant-smtp-password"
    }
  }'
```

## Usage Tracking

Track usage per tenant:

```bash
curl https://api.yourdomain.com/v1/tenants/tenant_abc123/usage \
  -H "Authorization: Bearer ADMIN_API_KEY"
```

Response:

```json
{
  "tenant_id": "tenant_abc123",
  "period": "2024-01",
  "emails_sent": 15420,
  "emails_delivered": 15234,
  "emails_bounced": 186,
  "api_requests": 18500
}
```

## Rate Limits

Set per-tenant rate limits:

```bash
curl -X PUT https://api.yourdomain.com/v1/tenants/tenant_abc123 \
  -H "Authorization: Bearer ADMIN_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "rate_limit": {
      "requests_per_minute": 200,
      "emails_per_day": 10000
    }
  }'
```

## Use Cases

### SaaS Platform

Your SaaS can give each customer their own tenant:

- Customers manage their own API keys
- Email metrics per customer
- Isolated templates and webhooks

### Multiple Projects

Separate your own projects:

- Production vs. staging environments
- Different brands or domains
- Departmental separation

### White-Label

Offer email sending to your clients:

- Each client gets their own tenant
- Configure their SMTP or use shared
- Bill based on usage metrics

## Database Schema

Tenants are stored in PostgreSQL:

```sql
-- Tenants table
CREATE TABLE tenants (
  id VARCHAR PRIMARY KEY,
  name VARCHAR NOT NULL,
  domain VARCHAR,
  settings JSONB,
  smtp_config JSONB,
  rate_limit JSONB,
  created_at TIMESTAMP DEFAULT NOW()
);

-- API keys reference tenant
CREATE TABLE api_keys (
  id VARCHAR PRIMARY KEY,
  tenant_id VARCHAR REFERENCES tenants(id),
  key_hash VARCHAR NOT NULL,
  permissions VARCHAR[],
  created_at TIMESTAMP DEFAULT NOW()
);

-- Emails belong to tenant
CREATE TABLE emails (
  id VARCHAR PRIMARY KEY,
  tenant_id VARCHAR REFERENCES tenants(id),
  -- ... other fields
);
```

## Next Steps

- [Authentication](/api/authentication) - API key management
- [Architecture](/concepts/architecture) - System design
