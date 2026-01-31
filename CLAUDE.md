# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SelfMX is a self-hosted email sending platform providing a Resend-compatible API backed by AWS SES. It automates domain verification with DNS record management via Cloudflare integration.

## Build & Test Commands

### Backend (.NET 9)

```bash
dotnet build SelfMX.slnx           # Build solution
dotnet test SelfMX.slnx            # Run all tests (XUnit)
dotnet test --filter "FullyQualifiedName~DomainService"  # Run specific test class
dotnet run --project src/SelfMX.Api  # Run API (port 5000)
```

### Frontend (React/Vite)

```bash
cd client
npm install                        # Install dependencies
npm run dev                        # Dev server (port 5173, proxies /v1 to :5000)
npm run build                      # TypeScript check + Vite build
npm run lint                       # ESLint
npm run test                       # Playwright E2E tests (headless)
npm run test:headed                # Playwright with browser UI
```

### Full Stack Development

Run both servers simultaneously:
- Terminal 1: `dotnet run --project src/SelfMX.Api`
- Terminal 2: `cd client && npm run dev`

Frontend at `http://localhost:5173` proxies API calls to backend at `http://localhost:5000`.

### Ando Build System

```bash
ando                      # Build backend, frontend, run tests
ando -p publish --dind    # Build + push Docker image to ghcr.io
ando verify               # Validate build script
ando clean                # Remove build artifacts
```

Find Ando documentation at https://andobuild.com

## Architecture

### Backend (ASP.NET Core Minimal APIs)

```
src/SelfMX.Api/
├── Program.cs              # DI setup, middleware, route registration
├── Endpoints/              # Minimal API route handlers
│   ├── DomainEndpoints.cs  # /v1/domains CRUD
│   └── EmailEndpoints.cs   # /v1/emails send
├── Services/               # Business logic
│   ├── DomainService.cs    # Domain CRUD, verification state
│   ├── SesService.cs       # AWS SES integration
│   ├── CloudflareService.cs # DNS record management
│   └── DnsVerificationService.cs # Direct DNS checks
├── Jobs/                   # Hangfire background jobs
│   ├── SetupDomainJob.cs   # Creates SES identity, DNS records
│   ├── VerifyDomainsJob.cs # Polls verification status (every 5 min)
│   └── CleanupSentEmailsJob.cs # Deletes old sent emails (daily at 3 AM)
├── Data/
│   ├── AppDbContext.cs     # EF Core DbContext (Domains, ApiKeys, ApiKeyDomains)
│   └── AuditDbContext.cs   # Separate audit log context
└── Authentication/         # API key auth + rate limiting
```

**Key patterns:**
- Routes use `TypedResults` for compile-time response type safety
- Domain verification state machine: Pending → Verifying → Verified/Failed
- DNS records stored as JSON in Domain entity
- Multi-provider database support: SQLite (default) or SQL Server

### Database Configuration

SelfMX supports two database providers:

| Provider | Config Value | Use Case |
|----------|--------------|----------|
| SQLite | `sqlite` (default) | Development, small deployments |
| SQL Server | `sqlserver` or `docker-sqlserver` | Enterprise, high concurrency |

**Configuration:**
```json
{
  "Database": {
    "Provider": "sqlite"  // or "sqlserver"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=selfmx.db",
    "AuditConnection": "Data Source=audit.db",
    "HangfireConnection": "selfmx-hangfire.db"
  }
}
```

**SQL Server mode:**
- Connection resilience with automatic retry (5 retries, 30s max delay)
- Shared database for main + audit + Hangfire (no lock contention issues)
- Scaled Hangfire workers (ProcessorCount * 2 vs 1 for SQLite)

**Migration from SQLite to SQL Server:**
- `GET /v1/migration/status` - Check if migration is needed
- `POST /v1/migration/start` - Execute migration (admin only)
- Creates backups before migrating, verifies row counts after

### Frontend (React 19 + TanStack Query)

```
client/src/
├── pages/DomainsPage.tsx   # Main view with domain list
├── components/
│   ├── ui/                 # Reusable primitives (Button, Card, Input)
│   └── Domain*.tsx         # Domain-specific components
├── hooks/useDomains.ts     # React Query hooks for API
└── lib/
    ├── api.ts              # ApiClient with Zod validation
    └── schemas.ts          # Zod schemas matching API responses
```

**Key patterns:**
- API responses validated with Zod schemas at runtime
- TanStack Query for server state management
- Tailwind CSS 4 with OKLCH color system
- Dark mode via React Context

### API Endpoints

| Endpoint | Auth | Description |
|----------|------|-------------|
| `GET /health` | No | Health check |
| `GET /` | No | Status |
| `POST /v1/domains` | Yes | Create domain |
| `GET /v1/domains` | Yes | List domains (paginated) |
| `GET /v1/domains/{id}` | Yes | Get domain |
| `DELETE /v1/domains/{id}` | Yes | Delete domain |
| `POST /v1/emails` | Yes | Send email (Resend-compatible) |
| `GET /v1/sent-emails` | Yes | List sent emails (keyset pagination) |
| `GET /v1/sent-emails/{id}` | Yes | Get sent email detail |
| `GET /v1/api-keys` | Admin | List API keys |
| `POST /v1/api-keys` | Admin | Create API key |
| `GET /v1/audit` | Admin | Audit logs (paginated) |
| `GET /v1/migration/status` | Admin | Migration status |
| `POST /v1/migration/start` | Admin | Start SQLite→SQL Server migration |

Auth: Bearer token with API key, or Cookie auth for admin UI.
Admin: Requires `ActorType=admin` claim (cookie auth or admin API key).

## Configuration

Environment variables or `appsettings.json`:

```json
{
  "App": {
    "ApiKeyHash": "<bcrypt hash of API key>",
    "SentEmailRetentionDays": 30  // null or 0 = keep forever
  },
  "Aws": {
    "Region": "us-east-1",
    "AccessKeyId": "<optional, uses IAM if blank>",
    "SecretAccessKey": "<optional>"
  },
  "Cloudflare": {
    "ApiToken": "<cloudflare api token>",
    "ZoneId": "<cloudflare zone id>"
  }
}
```

## Naming Convention

The project uses "SelfMX" casing (capital M and X) for:
- Namespace: `SelfMX.Api`
- Solution: `SelfMX.slnx`
- Project directories: `src/SelfMX.Api`, `tests/SelfMX.Api.Tests`
- User-facing brand text in UI and documentation
