---
layout: ../layouts/DocsLayout.astro
title: Changelog
description: Release notes and version history for SelfMX.
---

All notable changes to SelfMX are documented here.

## [0.9.3] - 2026-01-23

### Added
- SSH identity file support for remote installation (`-i` flag)
- Auto-commit option in bumpversion for uncommitted changes

### Changed
- Improved SSH installation documentation with Hetzner Ubuntu 24 guidance

---

## [1.0.0] - 2024-01-15

### Added

- Initial release of SelfMX
- REST API for sending transactional emails
- Multi-tenant support with isolated API keys
- Email templates with Handlebars syntax
- Webhook notifications for delivery events
- PostgreSQL database storage
- Docker deployment support
- Prometheus metrics endpoint
- Health check endpoint

### Features

- **Send Email API** - Simple HTTP endpoint for sending emails
- **Template System** - Create and manage email templates
- **Multi-Tenancy** - Support for multiple domains and API keys
- **Webhooks** - Real-time delivery notifications
- **Audit Trail** - Complete logging of all email activity
- **Rate Limiting** - Configurable request limits

### Documentation

- Getting Started guide
- Configuration reference
- API documentation
- Deployment guides
- Troubleshooting guide

---

## Future Releases

Planned features for upcoming releases:

- [ ] Email scheduling (send later)
- [ ] Batch sending API
- [ ] Email analytics dashboard
- [ ] S3/object storage for attachments
- [ ] DKIM signing
- [ ] Bounce management automation
- [ ] Template versioning
- [ ] A/B testing support

---

## Versioning

SelfMX follows [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API changes
- **MINOR** version for backwards-compatible features
- **PATCH** version for backwards-compatible bug fixes

## Upgrade Notes

When upgrading between versions, always:

1. Backup your database
2. Read the changelog for breaking changes
3. Test in staging environment
4. Run database migrations

## Contributing

Found a bug or have a feature request? [Open an issue](https://github.com/aduggleby/selfmx/issues) on GitHub.
