---
layout: ../../layouts/DocsLayout.astro
title: Getting Started
description: Learn how to set up and use SelfMX for sending transactional emails.
---

SelfMX is a self-hosted email API that lets you send transactional emails from your own infrastructure. This guide will help you get started quickly.

## Prerequisites

Before you begin, make sure you have:

- **Docker** installed on your server
- **PostgreSQL** database (can be run alongside SelfMX in Docker)
- A domain with DNS access for email sending
- SMTP credentials (your own mail server or a relay service)

## Quick Overview

SelfMX provides:

1. **REST API** - Simple HTTP endpoints for sending emails
2. **Multi-tenant support** - Manage multiple domains and API keys
3. **Template engine** - Dynamic email content with variables
4. **Webhooks** - Real-time delivery notifications
5. **Audit trail** - Complete logging for compliance

## Next Steps

- [Installation](/getting-started/installation) - Deploy SelfMX on your server
- [Quick Start](/getting-started/quick-start) - Send your first email
- [Basic Usage](/getting-started/basic-usage) - Learn the core API endpoints
