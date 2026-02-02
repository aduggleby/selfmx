# SelfMX

Self-hosted email sending platform with a Resend-compatible API, powered by AWS SES.

**[Documentation](https://selfmx.com)** · **[Getting Started](#getting-started)** · **[Development](#development)**

## Overview

SelfMX provides a drop-in replacement for Resend's email API that you can self-host. It handles domain verification, DNS record management (via Cloudflare), and email delivery through AWS SES.

**Key Features:**
- Resend-compatible API (`POST /v1/emails`)
- Automatic domain verification with DNS management
- Multi-tenant API keys with domain scoping
- Admin dashboard for key management and audit logs
- Automatic SSL via Caddy
- Daily backups with retention policies
- SQL Server database for production reliability

## Getting Started

### One-Line Install (Ubuntu 22.04+)

```bash
curl -fsSL https://raw.githubusercontent.com/aduggleby/selfmx/main/deploy/install.sh | sudo bash
```

The installer will:
1. Install Docker and dependencies
2. Deploy SQL Server 2022 container
3. Prompt for configuration (domain, AWS credentials, admin password)
4. Set up automatic SSL certificates
5. Configure daily backups
6. Start SelfMX as a systemd service

### Prerequisites

- Ubuntu 22.04+ server (4GB RAM minimum for SQL Server)
- Domain name pointed to your server
- AWS account with SES access
- (Optional) Cloudflare account for automatic DNS management

### Post-Install

1. **Configure DNS**: Point your domain to the server IP
   ```bash
   dig your-domain.com @8.8.8.8
   ```

2. **Access Admin UI**: `https://your-domain.com/admin/login`

3. **Create API Keys**: Generate keys scoped to specific domains

4. **Send Email**:
   ```bash
   curl -X POST https://your-domain.com/v1/emails \
     -H "Authorization: Bearer re_your_api_key" \
     -H "Content-Type: application/json" \
     -d '{
       "from": "hello@yourdomain.com",
       "to": "user@example.com",
       "subject": "Hello",
       "html": "<p>Hello from SelfMX!</p>"
     }'
   ```

### Management Commands

```bash
# View logs
docker compose -f /opt/selfmx/docker-compose.yml logs -f

# Restart services
sudo systemctl restart selfmx

# Manual backup
sudo selfmx-backup

# Restore from backup
sudo selfmx-restore selfmx_20260126_030000.bak

# Update to latest version
curl -fsSL https://raw.githubusercontent.com/aduggleby/selfmx/main/deploy/install.sh | sudo bash
```

## Repository Structure

```
selfmx/
├── src/SelfMX.Api/           # .NET 9 backend
│   ├── Endpoints/            # API route handlers
│   ├── Services/             # Business logic (SES, Cloudflare, DNS)
│   ├── Jobs/                 # Hangfire background jobs
│   ├── Entities/             # EF Core models
│   └── Authentication/       # API key auth, rate limiting
├── tests/SelfMX.Api.Tests/   # xUnit tests
├── client/                   # React frontend (Vite + TanStack Query)
│   ├── src/pages/            # Page components
│   ├── src/components/       # UI components
│   └── src/hooks/            # React Query hooks
├── website/                  # Documentation site (Astro)
├── deploy/                   # Deployment files
│   ├── install.sh            # One-line installer
│   └── docker-compose.yml    # Production compose file
├── docker-compose.dev.yml    # Development SQL Server
├── build.csando              # Ando build script
├── ando-pre.csando           # Pre-hook (cleanup, auth)
├── Dockerfile                # Multi-stage Docker build
└── mise.toml                 # .NET 9 version pinning
```

## Development

### Quick Start

```bash
# Clone and setup
git clone https://github.com/aduggleby/selfmx.git
cd selfmx
mise install              # Installs .NET 9

# Start development SQL Server
docker compose -f docker-compose.dev.yml up -d

# Run backend (terminal 1)
dotnet run --project src/SelfMX.Api

# Run frontend (terminal 2)
cd client && npm install && npm run dev
```

- Backend: http://localhost:17400
- Frontend: http://localhost:17401 (proxies API to backend)
- SQL Server: localhost:17402 (sa / Dev@Password123!)

### Prerequisites

- [mise](https://mise.jdx.dev/) - manages .NET version
- Node.js 22+
- Docker - for SQL Server and full builds

### Ando Build System

[Ando](https://andobuild.com) provides reproducible builds in Docker containers.

```bash
# Build and test everything
ando

# Full release: build, test, push Docker image to ghcr.io, deploy docs
ando --dind -p publish

# Validate build script
ando verify
```

## API Reference

SelfMX implements the [Resend API](https://resend.com/docs/api-reference/emails/send-email) for email sending.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/v1/system/status` | GET | System status (AWS, DB connectivity) |
| `/v1/emails` | POST | Send email |
| `/v1/domains` | GET | List domains |
| `/v1/domains` | POST | Add domain |
| `/v1/domains/{id}` | GET | Get domain |
| `/v1/domains/{id}` | DELETE | Delete domain |
| `/v1/domains/{id}/verify` | POST | Trigger verification check |
| `/v1/domains/{id}/test-email` | POST | Send test email |
| `/v1/api-keys` | GET | List API keys |
| `/v1/api-keys` | POST | Create API key |
| `/v1/audit` | GET | Audit logs |
| `/hangfire` | GET | Background jobs dashboard (admin) |

See [full documentation](https://selfmx.com) for details.

## License

No'Sassy - Free to use, modify, and distribute. Cannot be offered as a competing SaaS.
