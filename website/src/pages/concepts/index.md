---
layout: ../../layouts/DocsLayout.astro
title: Concepts
description: Understand how SelfMX works under the hood.
---

This section explains the core concepts and architecture of SelfMX.

## Overview

SelfMX is designed as a simple, self-hosted email sending service. It consists of:

1. **REST API** - HTTP endpoints for sending emails and managing resources
2. **Queue Processor** - Background workers that process email queue
3. **SMTP Sender** - Delivers emails through your SMTP server
4. **PostgreSQL Database** - Stores all data

## Core Concepts

### Transactional Email

Transactional emails are automated messages triggered by user actions:

- Welcome emails after registration
- Password reset links
- Order confirmations
- Notifications and alerts

Unlike marketing emails, transactional emails are:

- Sent individually (not in bulk)
- Time-sensitive
- Expected by the recipient

### Self-Hosted Benefits

Running your own email infrastructure provides:

- **Data Privacy** - Emails never leave your servers
- **Cost Control** - No per-email pricing
- **Customization** - Full control over configuration
- **Compliance** - Meet data residency requirements

## Learn More

- [Architecture](/concepts/architecture) - System design and components
- [Email Processing](/concepts/email-processing) - How emails are queued and sent
- [Multi-Tenancy](/concepts/tenants) - Managing multiple domains and API keys
