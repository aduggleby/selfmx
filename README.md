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

## Getting Started

### One-Line Install (Ubuntu 22.04+)

```bash
curl -fsSL https://raw.githubusercontent.com/aduggleby/selfmx/main/deploy/install.sh | sudo bash
```

The installer will:
1. Install Docker and dependencies
2. Prompt for configuration (domain, AWS credentials, admin password)
3. Set up automatic SSL certificates
4. Configure daily backups
5. Start SelfMX as a systemd service

### Prerequisites

- Ubuntu 22.04+ server (2GB RAM minimum)
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
sudo selfmx-restore /var/backups/selfmx/daily/selfmx-YYYY-MM-DD.tar.gz

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
│   ├── docker-compose.yml    # Production compose file
│   └── Caddyfile             # Reverse proxy config
├── scripts/                  # Development scripts
│   ├── selfmx-bumpversion.sh # Version management
│   └── selfmx-push.sh        # Build and release
├── build.csando              # Ando build script
├── Dockerfile                # Multi-stage Docker build
└── mise.toml                 # .NET 9 version pinning
```

## Development

### Prerequisites

- [mise](https://mise.jdx.dev/) (manages .NET version)
- Node.js 22+
- Docker (for full builds)

### Setup

```bash
# Clone repository
git clone https://github.com/aduggleby/selfmx.git
cd selfmx

# Install .NET 9 via mise
mise install

# Verify .NET version
dotnet --version  # Should show 9.x
```

### Running Locally

```bash
# Terminal 1: Backend
dotnet run --project src/SelfMX.Api

# Terminal 2: Frontend
cd client && npm install && npm run dev
```

- Backend: http://localhost:5000
- Frontend: http://localhost:5173 (proxies API to backend)

### Build Commands

```bash
# Build solution
dotnet build SelfMX.slnx

# Run tests
dotnet test SelfMX.slnx

# Build frontend
cd client && npm run build

# Lint frontend
cd client && npm run lint
```

### Ando Build System

[Ando](https://andobuild.com) provides reproducible builds in Docker containers.

```bash
# Build and test everything
ando

# Full release: build, test, push Docker image to ghcr.io, deploy docs
ando --dind -p push

# Validate build script
ando verify
```

### Version Management

```bash
# Bump patch version (1.0.0 -> 1.0.1)
./scripts/selfmx-bumpversion.sh

# Bump minor version (1.0.0 -> 1.1.0)
./scripts/selfmx-bumpversion.sh minor

# Bump major version (1.0.0 -> 2.0.0)
./scripts/selfmx-bumpversion.sh major
```

## API Reference

SelfMX implements the [Resend API](https://resend.com/docs/api-reference/emails/send-email) for email sending.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/v1/emails` | POST | Send email |
| `/v1/domains` | GET | List domains |
| `/v1/domains` | POST | Add domain |
| `/v1/domains/{id}` | GET | Get domain |
| `/v1/domains/{id}` | DELETE | Delete domain |
| `/v1/api-keys` | GET | List API keys |
| `/v1/api-keys` | POST | Create API key |
| `/admin/audit` | GET | Audit logs |

See [full documentation](https://selfmx.com) for details.

## License

MIT
