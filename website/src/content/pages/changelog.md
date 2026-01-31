---
title: Changelog
description: Release notes and version history for SelfMX.
toc: true
---

All notable changes to SelfMX are documented here.

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
