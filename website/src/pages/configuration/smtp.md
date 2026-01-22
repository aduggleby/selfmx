---
layout: ../../layouts/DocsLayout.astro
title: SMTP Settings
description: Configure email delivery through SMTP.
---

SelfMX sends emails through SMTP. Configure your SMTP server or use a relay service.

## Basic Settings

### SMTP_HOST

**Required.** SMTP server hostname.

```bash
SMTP_HOST=smtp.example.com
```

### SMTP_PORT

SMTP server port. Default: `587`

```bash
SMTP_PORT=587
```

Common ports:

- `25` - Standard SMTP (often blocked)
- `465` - SMTP over SSL
- `587` - SMTP with STARTTLS (recommended)
- `2525` - Alternative port

### SMTP_USER

SMTP authentication username.

```bash
SMTP_USER=apikey
```

### SMTP_PASS

SMTP authentication password.

```bash
SMTP_PASS=your-smtp-password
```

## Security Settings

### SMTP_TLS

Enable TLS encryption. Default: `true`

```bash
SMTP_TLS=true
```

### SMTP_TLS_VERIFY

Verify TLS certificates. Default: `true`

```bash
SMTP_TLS_VERIFY=true
```

Set to `false` only for development with self-signed certificates.

## Connection Settings

### SMTP_TIMEOUT

Connection timeout in seconds. Default: `30`

```bash
SMTP_TIMEOUT=30
```

### SMTP_POOL_SIZE

Connection pool size. Default: `10`

```bash
SMTP_POOL_SIZE=10
```

## Provider Examples

### SendGrid

```bash
SMTP_HOST=smtp.sendgrid.net
SMTP_PORT=587
SMTP_USER=apikey
SMTP_PASS=SG.your-api-key-here
SMTP_TLS=true
```

### Mailgun

```bash
SMTP_HOST=smtp.mailgun.org
SMTP_PORT=587
SMTP_USER=postmaster@your-domain.mailgun.org
SMTP_PASS=your-mailgun-password
SMTP_TLS=true
```

### Amazon SES

```bash
SMTP_HOST=email-smtp.us-east-1.amazonaws.com
SMTP_PORT=587
SMTP_USER=your-ses-smtp-username
SMTP_PASS=your-ses-smtp-password
SMTP_TLS=true
```

### Postmark

```bash
SMTP_HOST=smtp.postmarkapp.com
SMTP_PORT=587
SMTP_USER=your-postmark-server-api-token
SMTP_PASS=your-postmark-server-api-token
SMTP_TLS=true
```

### Self-Hosted (Postfix)

```bash
SMTP_HOST=mail.yourdomain.com
SMTP_PORT=587
SMTP_USER=noreply@yourdomain.com
SMTP_PASS=your-email-password
SMTP_TLS=true
```

## Testing SMTP

Verify your SMTP configuration:

```bash
curl -X POST http://localhost:8080/v1/test-smtp \
  -H "Authorization: Bearer YOUR_API_KEY"
```

Response:

```json
{
  "status": "ok",
  "message": "SMTP connection successful"
}
```

## Troubleshooting

### Connection Refused

- Verify the SMTP host and port
- Check firewall rules allow outbound connections
- Try alternative ports (587, 465, 2525)

### Authentication Failed

- Double-check username and password
- Some providers require app-specific passwords
- Verify the account has SMTP access enabled

### TLS Handshake Failed

- Ensure `SMTP_TLS=true` for port 587
- For port 465, the connection uses implicit TLS
- Check certificate validity

## Next Steps

- [Database Options](/configuration/database) - Database configuration
- [Troubleshooting](/guides/troubleshooting) - Common issues and solutions
