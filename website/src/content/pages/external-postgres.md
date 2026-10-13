---
title: External PostgreSQL Setup
description: Configure SelfMX to use an existing PostgreSQL server instead of the containerized one.
toc: true
---

## Overview

By default, SelfMX includes a containerized PostgreSQL 17 instance. You can instead connect to an existing PostgreSQL server for environments where you already have database infrastructure. SelfMX creates its tables automatically at startup, including the Hangfire job tables (in the `hangfire` schema).

## Create Database and User

Connect to your PostgreSQL server as a superuser:

```bash
psql -h YOUR_SERVER_IP -U postgres
```

Create a dedicated role that owns the database:

```sql
CREATE ROLE selfmx WITH LOGIN PASSWORD 'YourSecurePassword123';
CREATE DATABASE selfmx OWNER selfmx;
```

Type `\q` to quit psql.

## Connection String

Point SelfMX to your server. `Database__Provider` is optional when the connection string uses `Host=` (Postgres is inferred), but setting it explicitly is recommended:

```bash
Database__Provider=Postgres
ConnectionStrings__DefaultConnection=Host=your-server;Port=5432;Database=selfmx;Username=selfmx;Password=YourSecurePassword123
```

### Connection String Parameters

| Parameter | Description |
|-----------|-------------|
| `Host` | Hostname or IP of the PostgreSQL server |
| `Port` | Port (default: `5432`) |
| `Database` | Database name (`selfmx`) |
| `Username` | Role name |
| `Password` | Role password |
| `SSL Mode` | Set to `Require` for encrypted connections |
| `Maximum Pool Size` | Connection pool maximum (default: 100) |

## Security Considerations

- Use a dedicated role for SelfMX (not `postgres`)
- Use a strong, unique password
- Restrict network access to the PostgreSQL port (5432)
- Consider requiring TLS (`SSL Mode=Require`)

## Troubleshooting

### Connection Failed

1. Verify the host and port are correct
2. Test connectivity: `nc -zv YOUR_SERVER_IP 5432`
3. Check `pg_hba.conf` allows the SelfMX host to connect as the `selfmx` role
4. Check `listen_addresses` in `postgresql.conf` includes the interface SelfMX connects to
5. Verify the credentials: `psql -h YOUR_SERVER_IP -U selfmx -d selfmx`

### Permission Errors

The `selfmx` role must own the `selfmx` database (or at least be able to create schemas and tables in it):

```sql
SELECT datname, pg_get_userbyid(datdba) AS owner FROM pg_database WHERE datname = 'selfmx';
```

## Backups

See **[Database Backups](/backups)** for `pg_dump` / `pg_restore` procedures.
