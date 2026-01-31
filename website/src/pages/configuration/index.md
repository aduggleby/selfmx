---
layout: ../../layouts/DocsLayout.astro
title: Configuration
description: Configure SelfMX for your environment and requirements.
---

SelfMX is configured through environment variables. This section covers all available configuration options.

## Configuration Methods

### Environment Variables

The primary configuration method. Set variables in your shell, Docker compose file, or `.env` file:

```bash
export DATABASE_URL=postgres://user:pass@host/selfmx
export API_KEY=your-secret-key
```

### Docker Compose

```yaml
services:
  selfmx:
    image: ghcr.io/aduggleby/selfmx:latest
    environment:
      - DATABASE_URL=postgres://user:pass@db/selfmx
      - API_KEY=your-secret-key
```

### .env File

Create a `.env` file in your working directory:

```bash
DATABASE_URL=postgres://user:pass@host/selfmx
API_KEY=your-secret-key
SMTP_HOST=smtp.example.com
```

## Configuration Categories

- [Environment Variables](/configuration/environment) - Core settings and API configuration
- [SMTP Settings](/configuration/smtp) - Email delivery configuration
- [Database Options](/configuration/database) - PostgreSQL connection settings

## Quick Reference

| Variable       | Required | Default | Description          |
| -------------- | -------- | ------- | -------------------- |
| `DATABASE_URL` | Yes      | -       | PostgreSQL connection string |
| `API_KEY`      | Yes      | -       | API authentication key |
| `SMTP_HOST`    | Yes      | -       | SMTP server hostname |
| `PORT`         | No       | 8080    | HTTP API port        |
| `LOG_LEVEL`    | No       | info    | Logging verbosity    |

See individual configuration pages for complete details.
