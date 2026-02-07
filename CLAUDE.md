# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Address the user as "Mr. SelfMX".**

## Project Overview

SelfMX is a self-hosted email sending platform providing a Resend-compatible API (sending only) powered by AWS SES. It simplifies domain verification with optional Cloudflare DNS integration.

## Port Range

This project uses ports in the range **17400-17499**:

| Service | Port | Description |
|---------|------|-------------|
| Backend API | 17400 | .NET API server |
| Frontend Dev | 17401 | Vite dev server |
| SQL Server Dev | 17402 | Development database |

## Build & Test Commands

### Backend (.NET 9)

```bash
dotnet build SelfMX.slnx           # Build solution
dotnet test SelfMX.slnx            # Run all tests (XUnit)
dotnet test --filter "FullyQualifiedName~DomainService"  # Run specific test class
dotnet run --project src/SelfMX.Api  # Run API (port 17400)
```

### Frontend (React/Vite)

```bash
cd client
npm install                        # Install dependencies
npm run dev                        # Dev server (port 17401, proxies API routes to :17400)
npm run build                      # TypeScript check + Vite build
npm run lint                       # ESLint
npm run test                       # Playwright E2E tests (headless)
npm run test:headed                # Playwright with browser UI
npx playwright test --config playwright.backend.config.ts  # Backend E2E tests (builds UI + runs .NET API)
```

### Full Stack Development

Run both servers simultaneously:
- Terminal 1: `docker compose -f docker-compose.dev.yml up -d` (start SQL Server)
- Terminal 2: `dotnet run --project src/SelfMX.Api`
- Terminal 3: `cd client && npm run dev`

Frontend at `http://localhost:17401` proxies API calls to backend at `http://localhost:17400`.

### Ando Build System

```bash
ando                      # Build backend, frontend, run tests
ando -p publish --dind    # Build + push Docker image to ghcr.io
ando clean                # Remove build artifacts
```

Find Ando documentation at https://andobuild.com/llms.txt

**Build logs:** The ando build log is written to `build.csando.log` in the same directory as the build file (`build.csando`).

## Architecture

### Backend (ASP.NET Core Minimal APIs)

```
src/SelfMX.Api/
├── Program.cs              # DI setup, middleware, route registration
├── Endpoints/              # Minimal API route handlers
│   ├── DomainEndpoints.cs  # /domains CRUD
│   └── EmailEndpoints.cs   # /emails send, get, list, batch
├── Services/               # Business logic
│   ├── DomainService.cs    # Domain CRUD, verification state
│   ├── SesService.cs       # AWS SES integration
│   ├── CloudflareService.cs # DNS record management
│   └── DnsVerificationService.cs # Direct DNS checks
├── Jobs/                   # Hangfire background jobs
│   ├── SetupDomainJob.cs   # Creates SES identity, DNS records
│   └── VerifyDomainsJob.cs # Polls verification status (every 5 min)
├── Data/
│   ├── AppDbContext.cs     # EF Core DbContext (Domains, ApiKeys, ApiKeyDomains)
│   └── AuditDbContext.cs   # Separate audit log context
└── Authentication/         # API key auth + rate limiting
```

**Key patterns:**
- Routes use `TypedResults` for compile-time response type safety
- Domain verification state machine: Pending → Verifying → Verified/Failed
- DNS records stored as JSON in Domain entity
- SQL Server only - single connection string for all components

### Database Configuration

SelfMX uses SQL Server as the only supported database.

**Configuration:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,17402;Database=SelfMX;User Id=sa;Password=...;TrustServerCertificate=True"
  }
}
```

**Features:**
- Connection resilience with automatic retry (5 retries, 30s max delay)
- Shared database for main + audit + Hangfire
- Scaled Hangfire workers (ProcessorCount * 2, capped at 20)
- Connection pooling (Max=200, Min=20)

**Development SQL Server:**
```bash
docker compose -f docker-compose.dev.yml up -d
# Credentials: sa / Dev@Password123!
# Port: 17402
```

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
| `GET /` | No | Redirects to `/ui/` |
| `GET /system/status` | No | System config validation (AWS, DB connectivity) |
| `GET /system/version` | No | API version and build info |
| `GET /system/logs` | Admin | In-memory application logs for diagnostics |
| `POST /domains` | Yes | Create domain |
| `GET /domains` | Yes | List domains (paginated) |
| `GET /domains/{id}` | Yes | Get domain |
| `DELETE /domains/{id}` | Yes | Delete domain |
| `POST /domains/{id}/verify` | Yes | Trigger manual verification check |
| `POST /domains/{id}/test-email` | Yes | Send test email (verified domains) |
| `POST /emails` | Yes | Send email (Resend-compatible) |
| `GET /emails/{id}` | Yes | Get sent email (Resend-compatible) |
| `GET /emails` | Yes | List sent emails (cursor-based pagination) |
| `POST /emails/batch` | Yes | Send batch emails |
| `GET /api-keys` | Admin | List API keys |
| `POST /api-keys` | Admin | Create API key |
| `GET /api-keys/revoked` | Admin | List archived API keys |
| `GET /audit` | Admin | Audit logs (paginated) |
| `GET /hangfire` | Admin | Hangfire job dashboard |

Auth: Bearer token with API key, or Cookie auth for admin UI.
Admin: Requires `ActorType=admin` claim (cookie auth or admin API key).

## Configuration

Environment variables or `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=SelfMX;..."
  },
  "App": {
    "Fqdn": "mail.example.com",
    "AdminPasswordHash": "$6$..." // SHA-512 crypt hash, generate with: openssl passwd -6 "YourPassword"
  },
  "Aws": {
    "Region": "us-east-1",
    "AccessKeyId": "<optional, uses IAM if blank>",
    "SecretAccessKey": "<optional>"
  },
  "Cloudflare": {
    "ApiToken": "<optional, for auto DNS>",
    "ZoneId": "<optional>"
  }
}
```

CORS origins are derived from `App:Fqdn` (becomes `https://{Fqdn}`). Falls back to `http://localhost:17401` for development.

## Naming Convention

The project uses "SelfMX" casing (capital M and X) for:
- Namespace: `SelfMX.Api`
- Solution: `SelfMX.slnx`
- Project directories: `src/SelfMX.Api`, `tests/SelfMX.Api.Tests`
- User-facing brand text in UI and documentation
