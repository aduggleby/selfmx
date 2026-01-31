---
layout: ../../layouts/DocsLayout.astro
title: Database Options
description: Configure PostgreSQL database connection and settings.
---

SelfMX uses PostgreSQL for storing emails, templates, API keys, and audit logs.

## Connection String

### DATABASE_URL

**Required.** PostgreSQL connection string.

```bash
DATABASE_URL=postgres://user:password@localhost:5432/selfmx
```

Format:

```
postgres://[user]:[password]@[host]:[port]/[database]?[options]
```

## Connection Options

### SSL Mode

For secure connections, add `sslmode` parameter:

```bash
DATABASE_URL=postgres://user:pass@host/db?sslmode=require
```

Options:

- `disable` - No SSL
- `require` - Require SSL, skip verification
- `verify-ca` - Require SSL, verify CA
- `verify-full` - Require SSL, verify CA and hostname

### Connection Pool

Configure connection pooling:

```bash
DATABASE_URL=postgres://user:pass@host/db?pool_size=20&pool_timeout=30
```

## Pool Settings

### DB_POOL_SIZE

Maximum connections in the pool. Default: `10`

```bash
DB_POOL_SIZE=20
```

### DB_POOL_TIMEOUT

Timeout waiting for a connection in seconds. Default: `30`

```bash
DB_POOL_TIMEOUT=30
```

### DB_IDLE_TIMEOUT

Close idle connections after seconds. Default: `300`

```bash
DB_IDLE_TIMEOUT=300
```

## Migrations

SelfMX automatically runs database migrations on startup.

### DB_AUTO_MIGRATE

Auto-run migrations. Default: `true`

```bash
DB_AUTO_MIGRATE=true
```

### Manual Migrations

Run migrations manually:

```bash
docker run --rm \
  -e DATABASE_URL=postgres://user:pass@host/db \
  ghcr.io/aduggleby/selfmx:latest \
  migrate
```

## Docker Compose Example

Complete database setup with Docker Compose:

```yaml
version: "3.8"

services:
  selfmx:
    image: ghcr.io/aduggleby/selfmx:latest
    environment:
      - DATABASE_URL=postgres://selfmx:password@db:5432/selfmx
    depends_on:
      db:
        condition: service_healthy

  db:
    image: postgres:15-alpine
    environment:
      - POSTGRES_USER=selfmx
      - POSTGRES_PASSWORD=password
      - POSTGRES_DB=selfmx
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U selfmx"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  pgdata:
```

## Production Recommendations

### Use Managed PostgreSQL

For production, consider managed PostgreSQL services:

- **AWS RDS** - Amazon Relational Database Service
- **Google Cloud SQL** - Fully managed PostgreSQL
- **DigitalOcean Managed Databases** - Simple setup
- **Supabase** - PostgreSQL with additional features

### Enable SSL

Always use SSL in production:

```bash
DATABASE_URL=postgres://user:pass@host/db?sslmode=verify-full
```

### Regular Backups

Set up automated backups. For Docker:

```bash
docker exec -t postgres pg_dump -U selfmx selfmx > backup.sql
```

### Monitor Connections

Monitor database connections to prevent exhaustion:

```sql
SELECT count(*) FROM pg_stat_activity WHERE datname = 'selfmx';
```

## Troubleshooting

### Connection Refused

- Verify PostgreSQL is running
- Check host and port
- Ensure network connectivity (Docker networks, firewalls)

### Authentication Failed

- Verify username and password
- Check PostgreSQL `pg_hba.conf` allows connections

### Too Many Connections

- Increase `max_connections` in PostgreSQL
- Reduce `DB_POOL_SIZE`
- Check for connection leaks

## Next Steps

- [Architecture](/concepts/architecture) - How SelfMX uses the database
- [Monitoring](/guides/monitoring) - Database metrics and alerts
