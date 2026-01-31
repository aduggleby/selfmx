---
layout: ../../layouts/DocsLayout.astro
title: Troubleshooting
description: Diagnose and fix common SelfMX issues.
---

Solutions to common issues with SelfMX.

## Connection Issues

### Cannot Connect to API

**Symptoms:**

- Connection refused
- Timeout errors

**Solutions:**

1. Check if SelfMX is running:

   ```bash
   docker ps | grep selfmx
   ```

2. Verify port binding:

   ```bash
   curl http://localhost:8080/health
   ```

3. Check Docker logs:

   ```bash
   docker logs selfmx
   ```

4. Verify firewall allows traffic on port 8080

### Database Connection Failed

**Symptoms:**

- "Connection refused" in logs
- "Authentication failed"

**Solutions:**

1. Verify DATABASE_URL format:

   ```bash
   # Correct format
   DATABASE_URL=postgres://user:password@host:5432/database
   ```

2. Test connection from container:

   ```bash
   docker exec -it selfmx pg_isready -h db -U selfmx
   ```

3. Check PostgreSQL is running:

   ```bash
   docker logs db
   ```

4. Verify credentials match between services

### SMTP Connection Failed

**Symptoms:**

- "Connection timeout" errors
- "Authentication failed"
- Emails stuck in queue

**Solutions:**

1. Test SMTP endpoint:

   ```bash
   curl -X POST http://localhost:8080/v1/test-smtp \
     -H "Authorization: Bearer YOUR_API_KEY"
   ```

2. Verify SMTP settings:

   ```bash
   SMTP_HOST=smtp.example.com
   SMTP_PORT=587
   SMTP_TLS=true
   ```

3. Try different ports (587, 465, 2525)

4. Check provider requires app-specific password

## Authentication Errors

### Invalid API Key

**Symptoms:**

- 401 Unauthorized responses
- "Invalid API key" errors

**Solutions:**

1. Verify key format in header:

   ```bash
   -H "Authorization: Bearer YOUR_API_KEY"
   ```

2. Check key matches environment variable:

   ```bash
   echo $API_KEY
   ```

3. Ensure no extra spaces or newlines

4. Create a new API key if needed

### Rate Limited

**Symptoms:**

- 429 Too Many Requests
- `X-RateLimit-Remaining: 0` header

**Solutions:**

1. Wait for rate limit window to reset

2. Check rate limit headers:

   ```bash
   X-RateLimit-Limit: 100
   X-RateLimit-Reset: 1705312260
   ```

3. Increase rate limits:

   ```bash
   RATE_LIMIT_REQUESTS=200
   ```

4. Distribute requests across multiple API keys

## Email Delivery Issues

### Emails Stuck in Queue

**Symptoms:**

- Emails show `queued` status
- Queue depth keeps growing

**Solutions:**

1. Check queue workers are running:

   ```bash
   docker logs selfmx | grep "worker"
   ```

2. Verify SMTP connectivity

3. Check for database locks:

   ```sql
   SELECT * FROM pg_locks WHERE NOT granted;
   ```

4. Restart workers:

   ```bash
   docker restart selfmx
   ```

### High Bounce Rate

**Symptoms:**

- Many emails bouncing
- Complaints from recipients

**Solutions:**

1. Verify sender domain DNS:

   - SPF record configured
   - DKIM record configured
   - DMARC policy set

2. Check if IP is blacklisted:

   ```bash
   # Check major blacklists
   dig +short your-ip.zen.spamhaus.org
   ```

3. Review bounce reasons in logs

4. Clean email lists (remove invalid addresses)

### Emails Going to Spam

**Solutions:**

1. Configure email authentication:

   ```dns
   # SPF Record
   v=spf1 include:_spf.yourdomain.com ~all

   # DKIM Record
   selector._domainkey.yourdomain.com

   # DMARC Record
   v=DMARC1; p=quarantine; rua=mailto:dmarc@yourdomain.com
   ```

2. Check sender reputation

3. Avoid spam trigger words in content

4. Include unsubscribe link

5. Send from recognized domain

## Performance Issues

### Slow API Response

**Symptoms:**

- High latency on API calls
- Timeouts

**Solutions:**

1. Check database query performance:

   ```sql
   SELECT * FROM pg_stat_activity
   WHERE state = 'active';
   ```

2. Add database indexes:

   ```sql
   CREATE INDEX idx_emails_status ON emails(status);
   CREATE INDEX idx_emails_created ON emails(created_at);
   ```

3. Increase database connections:

   ```bash
   DB_POOL_SIZE=20
   ```

4. Scale API servers horizontally

### High Memory Usage

**Solutions:**

1. Reduce worker count:

   ```bash
   QUEUE_WORKERS=2
   ```

2. Set memory limits in Docker:

   ```yaml
   deploy:
     resources:
       limits:
         memory: 512M
   ```

3. Check for memory leaks in logs

## Debugging

### Enable Debug Logging

```bash
LOG_LEVEL=debug
```

### View Real-time Logs

```bash
docker logs -f selfmx
```

### Check Database State

```sql
-- Queue status
SELECT status, COUNT(*)
FROM emails
GROUP BY status;

-- Recent errors
SELECT * FROM emails
WHERE status = 'failed'
ORDER BY created_at DESC
LIMIT 10;
```

### Test Individual Components

```bash
# Health check
curl http://localhost:8080/health

# SMTP test
curl -X POST http://localhost:8080/v1/test-smtp \
  -H "Authorization: Bearer YOUR_API_KEY"

# Send test email
curl -X POST http://localhost:8080/v1/send \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"from":"test@domain.com","to":"test@example.com","subject":"Test","text":"Test"}'
```

## Getting Help

If you're still stuck:

1. Check [GitHub Issues](https://github.com/aduggleby/selfmx/issues) for similar problems
2. Enable debug logging and collect logs
3. Open a new issue with:
   - SelfMX version
   - Configuration (redact secrets)
   - Error messages
   - Steps to reproduce
