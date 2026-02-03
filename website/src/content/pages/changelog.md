---
title: Changelog
description: Release notes and version history for SelfMX.
toc: true
---

All notable changes to SelfMX are documented here.

## [0.9.30] - 2026-02-03

### Added
- Automatic database schema updates on startup for smoother upgrades

## [0.9.29] - 2026-02-02

### Fixed
- Improved session handling when API key is invalidated or session expires

## [0.9.28] - 2026-02-02

### Added
- Manual verification check button to re-check domain status on demand
- New API endpoint `POST /v1/domains/{id}/verify` for triggering verification checks
- Verification status tracking with `lastCheckedAt` and `nextCheckAt` timestamps in domain responses

## [0.9.27] - 2026-02-02

### Added
- Delete confirmation dialog to prevent accidental domain deletion
- Moved delete action to domain detail page for safer access

## [0.9.26] - 2026-02-02

### Added
- **System Status Endpoint** (`GET /v1/system/status`) - Validates AWS credentials and database connectivity. Helps diagnose configuration issues immediately after deployment.
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
- [ ] Batch sending API
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
