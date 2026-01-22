---
layout: ../../layouts/DocsLayout.astro
title: Architecture
description: SelfMX system architecture and components.
---

Understanding SelfMX's architecture helps you deploy and scale effectively.

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         YOUR APPLICATION                        │
│                     (sends API requests)                        │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                            SelfMX                               │
│  ┌───────────────┐    ┌───────────────┐    ┌───────────────┐   │
│  │   REST API    │───▶│     Queue     │───▶│     SMTP      │   │
│  │   Server      │    │   Processor   │    │    Sender     │   │
│  └───────────────┘    └───────────────┘    └───────────────┘   │
│         │                    │                    │             │
│         └────────────────────┼────────────────────┘             │
│                              ▼                                  │
│                    ┌───────────────────┐                        │
│                    │    PostgreSQL     │                        │
│                    │     Database      │                        │
│                    └───────────────────┘                        │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
                         SMTP Server / Relay
                                │
                                ▼
                           Recipient
```

## Components

### REST API Server

The API server handles incoming HTTP requests:

- **Authentication** - Validates API keys
- **Validation** - Checks request parameters
- **Rate Limiting** - Enforces request limits
- **Logging** - Records all API activity

Endpoints include:

- `POST /v1/send` - Send email
- `GET /v1/emails` - List emails
- `POST /v1/templates` - Create template
- `POST /v1/webhooks` - Configure webhooks

### Queue Processor

Background workers process the email queue:

- **Pickup** - Fetches queued emails from database
- **Template Rendering** - Applies template variables
- **Retry Logic** - Handles transient failures
- **Status Updates** - Marks emails as sent/failed

Configuration:

```bash
QUEUE_WORKERS=4        # Number of workers
QUEUE_RETRY_ATTEMPTS=3 # Retry count
QUEUE_RETRY_DELAY=60   # Seconds between retries
```

### SMTP Sender

Delivers emails through SMTP:

- **Connection Pool** - Maintains SMTP connections
- **TLS Support** - Secure connections
- **Error Handling** - Parses SMTP responses

### PostgreSQL Database

Stores all persistent data:

| Table        | Purpose                           |
| ------------ | --------------------------------- |
| `emails`     | Email records and status          |
| `templates`  | Email templates                   |
| `api_keys`   | API authentication keys           |
| `webhooks`   | Webhook configurations            |
| `audit_logs` | Activity audit trail              |
| `tenants`    | Multi-tenant configurations       |

## Data Flow

### Sending an Email

1. **API Request** - Application sends POST to `/v1/send`
2. **Validation** - API validates request and API key
3. **Queue** - Email saved to database with `queued` status
4. **Response** - API returns email ID immediately
5. **Processing** - Worker picks up email from queue
6. **Template** - If using template, variables are rendered
7. **SMTP** - Email sent through SMTP server
8. **Status** - Database updated with delivery status
9. **Webhook** - If configured, delivery event sent to webhook

### Request Lifecycle

```
Request → Auth → Validate → Queue → Response (async)
                              ↓
                           Worker → Render → SMTP → Status → Webhook
```

## Scaling

### Horizontal Scaling

Scale API servers behind a load balancer:

```
                    Load Balancer
                          │
            ┌─────────────┼─────────────┐
            ▼             ▼             ▼
        SelfMX #1    SelfMX #2    SelfMX #3
            └─────────────┼─────────────┘
                          ▼
                     PostgreSQL
```

### Worker Scaling

Increase workers for higher throughput:

```bash
QUEUE_WORKERS=16
```

### Database Scaling

For high volume:

- Use managed PostgreSQL (RDS, Cloud SQL)
- Enable read replicas for reporting
- Consider partitioning `emails` table by date

## High Availability

### Redundancy

- Run multiple API instances
- Use PostgreSQL with replication
- Deploy across availability zones

### Monitoring

Monitor these metrics:

- API response times
- Queue depth (pending emails)
- SMTP success rate
- Database connections

See [Monitoring](/guides/monitoring) for setup details.

## Next Steps

- [Email Processing](/concepts/email-processing) - Queue and delivery details
- [Deployment](/guides/deployment) - Production deployment guide
