---
title: Changelog
description: Release notes and version history for SelfMX.
toc: true
---

All notable changes to SelfMX are documented here.

## [0.9.51] - 2026-02-07

### Added
- Add token management API endpoints

### Fixed
- Improve test reliability with better database isolation

## [0.9.50] - 2026-02-07

### Fixed
- Fix compatibility issues with certain reverse proxy configurations

## [0.9.49] - 2026-02-07

### Changed
- Internal improvements

## [0.9.48] - 2026-02-07

### Added
- **Get Email endpoint** (`GET /emails/{id}`) - Retrieve a sent email by ID with Resend-compatible response format
- **List Emails endpoint** (`GET /emails`) - List sent emails with cursor-based pagination (`before`, `after`, `limit` parameters)
- **Batch Send endpoint** (`POST /emails/batch`) - Send multiple emails in a single request with strict or permissive validation modes via `x-batch-validation` header

### Changed
- **Admin UI moved to `/ui/`** - The admin dashboard is now served under `/ui/` instead of the root path. The root `/` redirects to `/ui/`. This avoids collisions between UI routes and API routes when hosted on the same domain.
- **Resend-compatible error responses** - All API errors now return a structured format with `statusCode`, `name`, `message`, and `error` fields, matching the Resend API error format for better SDK compatibility

## [0.9.47] - 2026-02-06

### Improved
- Improve email send API response consistency

## [0.9.46] - 2026-02-06

### Improved
- Improve email send response format for better compatibility with official Resend SDKs

## [0.9.45] - 2026-02-06

### Fixed
- Fix email send response to return a standard ID format, improving compatibility with official Resend SDKs

## [0.9.44] - 2026-02-06

### Changed
- **API routes simplified** - Removed `/v1` prefix from all endpoints (e.g., `/v1/emails` → `/emails`). This improves compatibility with official Resend SDKs that expect root-level paths.
- **Container port updated** - Internal container port changed from 5000 to 17400 (SelfMX reserved port range). Update your `docker-compose.yml` port mappings if you use a custom deployment.

## [0.9.43] - 2026-02-03

### Added
- Show which API key was used when viewing sent emails

### Fixed
- Improve organization of revoked API keys in a collapsible section

## [0.9.42] - 2026-02-03

### Added
- Auto-archive revoked API keys after 90 days to keep your key list clean

## [0.9.41] - 2026-02-03

### Changed
- Internal improvements

## [0.9.40] - 2026-02-03

### Changed
- Internal improvements

## [0.9.39] - 2026-02-03

### Changed
- Require authentication by default for all API endpoints

## [0.9.38] - 2026-02-03

### Added
- Add version and diagnostics endpoint for troubleshooting server issues

## [0.9.37] - 2026-02-03

### Fixed
- Fix authentication to properly support both API key and session-based access

## [0.9.36] - 2026-02-03

### Changed
- Internal improvements

## [0.9.35] - 2026-02-03

### Fixed
- Fix API key domain scoping to properly restrict access to authorized domains only

## [0.9.34] - 2026-02-03

### Added
- **API Keys Management UI** - View, create, and revoke API keys from the admin dashboard
- **Sent Emails UI** - Browse sent emails with filtering by domain, sender, and recipient
- **Sent Emails API** - New endpoints `GET /sent-emails` and `GET /sent-emails/{id}` for accessing email history
- **Revoke API Key endpoint** - `DELETE /api-keys/{id}` for revoking keys via API

## [0.9.33] - 2026-02-03

### Added
- Verify SPF and DMARC TXT records during domain verification
- Fallback DNS verification via Cloudflare when direct lookups fail

## [0.9.32] - 2026-02-03

### Changed
- Simplify domain detail page by removing manual verification check button

## [0.9.31] - 2026-02-03

### Fixed
- Fix DNS record names in BIND file export to use correct relative format

## [0.9.30] - 2026-02-03

### Added
- Automatic database schema updates on startup for smoother upgrades

## [0.9.29] - 2026-02-02

### Fixed
- Improved session handling when API key is invalidated or session expires

## [0.9.28] - 2026-02-02

### Added
- Manual verification check button to re-check domain status on demand
- New API endpoint `POST /domains/{id}/verify` for triggering verification checks
- Verification status tracking with `lastCheckedAt` and `nextCheckAt` timestamps in domain responses

## [0.9.27] - 2026-02-02

### Added
- Delete confirmation dialog to prevent accidental domain deletion
- Moved delete action to domain detail page for safer access

## [0.9.26] - 2026-02-02

### Added
- **System Status Endpoint** (`GET /system/status`) - Validates AWS credentials and database connectivity. Helps diagnose configuration issues immediately after deployment.
- **Hangfire Dashboard** - Background job monitoring at `/hangfire` (now available in production, requires admin authentication). View job status, failures, and processing metrics.
- **Configuration Error Modal** - The admin UI now displays a blocking modal when critical configuration is missing (AWS credentials, database connection), guiding users to fix issues.

### Fixed
- Improved error logging during domain setup for easier troubleshooting
- DateTime serialization now consistently uses UTC with ISO 8601 format

## [0.9.22] - 2026-02-02

### Changed
- Simplify user interface with a cleaner, more compact design

## [0.9.21] - 2026-02-02

### Fixed
- Fix API authentication for admin dashboard sessions

## [0.9.19] - 2026-02-02

### Changed
- Internal improvements to password verification

## [0.9.18] - 2026-02-02

### Changed
- Cleaner startup output with improved logging

## [0.9.17] - 2026-02-01

### Changed
- Updated browser tab title and favicon

### Fixed
- Improve Docker image reliability on ARM64 systems
- Fix compatibility issues on certain Linux configurations

## [0.9.15] - 2026-02-01

### Added
- Multi-architecture Docker images (AMD64 and ARM64) - now runs on AWS Graviton, Apple Silicon, and ARM servers

### Fixed
- Improve reliability of Docker image publishing with atomic build+push

## [0.9.14] - 2026-01-31

### Added
- Display version and server information on startup

## [0.9.13] - 2026-01-31

### Added
- Comprehensive AWS SES setup guide

### Fixed
- SQL Server compatibility on all system configurations

## [0.9.10] - 2026-01-31

### Added
- Send test email feature for verifying domain configuration

### Changed
- Improved security with SHA-512 password hashing for admin accounts
- Simplified database support to SQL Server only

### Fixed
- Build system improvements for release publishing

## [0.9.4] - 2026-01-23

### Fixed
- Bumpversion now checks GitHub authentication before attempting to push to remote

## [0.9.3] - 2026-01-23

### Added
- SSH identity file support for remote installation (`-i` flag)
- Auto-commit option in bumpversion for uncommitted changes

### Changed
- Improved SSH installation documentation with Hetzner Ubuntu 24 guidance

## [1.0.0] - 2024-01-15

### Added

- Initial release of SelfMX
- Resend-compatible REST API for sending transactional emails (sending only)
- Multi-tenant API key support with domain scoping
- AWS SES integration for reliable email delivery
- Domain verification with Cloudflare DNS integration (optional)
- SQL Server database storage
- Docker deployment with Caddy reverse proxy
- Automated backup system for SQL Server
- Health check endpoint

### Features

- **Send Email API** - Resend-compatible HTTP endpoint
- **Domain Management** - Add, verify, and manage sending domains
- **Multi-Tenancy** - Scope API keys to specific domains
- **Audit Trail** - Complete logging of all email activity
- **Rate Limiting** - Configurable request limits per API key

### Documentation

- Installation guide for Ubuntu servers
- TrueNAS SCALE installation guide
- API reference
- Configuration documentation

## Future Releases

Planned features for upcoming releases:

- [ ] Email scheduling (send later)
- [x] Batch sending API
- [ ] Email analytics dashboard
- [ ] DKIM key rotation
- [ ] Bounce management automation
- [ ] Webhook notifications for delivery events

## Versioning

SelfMX follows [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API changes
- **MINOR** version for backwards-compatible features
- **PATCH** version for backwards-compatible bug fixes

## Upgrade Notes

When upgrading between versions:

1. Backup your database with `selfmx-backup`
2. Read the changelog for breaking changes
3. Run the installer with **[U] Update** option
4. Verify the health check passes

## Contributing

Found a bug or have a feature request? [Open an issue](https://github.com/aduggleby/selfmx/issues) on GitHub.
