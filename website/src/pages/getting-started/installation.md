---
layout: ../../layouts/DocsLayout.astro
title: Installation
description: Deploy SelfMX using Docker on your server.
---

SelfMX is distributed as a Docker image for easy deployment. This guide covers the installation process.

## Docker Installation

### Pull the Image

```bash
docker pull ghcr.io/aduggleby/selfmx:latest
```

### Basic Setup

Create a `docker-compose.yml` file:

```yaml
version: "3.8"
services:
  selfmx:
    image: ghcr.io/aduggleby/selfmx:latest
    ports:
      - "8080:8080"
      - "2525:2525"
    environment:
      - DATABASE_URL=postgres://selfmx:password@db/selfmx
      - API_KEY=your-secure-api-key
      - SMTP_HOST=smtp.yourdomain.com
      - SMTP_PORT=587
      - SMTP_USER=your-smtp-user
      - SMTP_PASS=your-smtp-password
    depends_on:
      - db

  db:
    image: postgres:15-alpine
    environment:
      - POSTGRES_USER=selfmx
      - POSTGRES_PASSWORD=password
      - POSTGRES_DB=selfmx
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

### Start the Services

```bash
docker compose up -d
```

SelfMX will be available at `http://localhost:8080`.

## Verify Installation

Check that the API is running:

```bash
curl http://localhost:8080/health
```

You should receive:

```json
{ "status": "ok" }
```

## Production Considerations

For production deployments:

1. **Use a reverse proxy** (nginx, Caddy) with HTTPS
2. **Set secure environment variables** - Don't hardcode secrets
3. **Configure database backups** - Regular PostgreSQL backups
4. **Set up monitoring** - Track API response times and error rates

## Next Steps

- [Quick Start](/getting-started/quick-start) - Send your first email
- [Environment Variables](/configuration/environment) - Full configuration reference
