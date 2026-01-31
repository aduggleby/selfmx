---
layout: ../../layouts/DocsLayout.astro
title: Monitoring
description: Set up monitoring and alerting for SelfMX.
---

Monitor your email infrastructure to ensure reliability and catch issues early.

## Metrics Endpoint

SelfMX exposes Prometheus metrics at `/metrics`:

```bash
curl http://localhost:8080/metrics
```

## Key Metrics

### Email Metrics

| Metric                        | Type    | Description                    |
| ----------------------------- | ------- | ------------------------------ |
| `selfmx_emails_total`         | Counter | Total emails processed         |
| `selfmx_emails_queued`        | Gauge   | Current queue depth            |
| `selfmx_emails_delivered`     | Counter | Successfully delivered         |
| `selfmx_emails_bounced`       | Counter | Bounced emails                 |
| `selfmx_emails_failed`        | Counter | Failed after retries           |

### API Metrics

| Metric                        | Type      | Description                  |
| ----------------------------- | --------- | ---------------------------- |
| `selfmx_api_requests_total`   | Counter   | Total API requests           |
| `selfmx_api_request_duration` | Histogram | Request latency              |
| `selfmx_api_errors_total`     | Counter   | API errors by code           |

### SMTP Metrics

| Metric                        | Type      | Description                  |
| ----------------------------- | --------- | ---------------------------- |
| `selfmx_smtp_connections`     | Gauge     | Active SMTP connections      |
| `selfmx_smtp_send_duration`   | Histogram | SMTP send latency            |
| `selfmx_smtp_errors_total`    | Counter   | SMTP errors                  |

## Prometheus Configuration

Add SelfMX to your `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: "selfmx"
    static_configs:
      - targets: ["selfmx:8080"]
    metrics_path: /metrics
    scrape_interval: 15s
```

## Grafana Dashboard

Import the SelfMX Grafana dashboard for visualizing metrics.

### Key Panels

1. **Email Volume** - Emails sent per minute
2. **Delivery Rate** - Percentage delivered vs. bounced
3. **Queue Depth** - Pending emails over time
4. **API Latency** - P50, P95, P99 response times
5. **Error Rate** - Errors per minute

### Example Dashboard JSON

```json
{
  "title": "SelfMX Overview",
  "panels": [
    {
      "title": "Emails Sent",
      "type": "graph",
      "targets": [
        {
          "expr": "rate(selfmx_emails_total[5m])",
          "legendFormat": "emails/sec"
        }
      ]
    },
    {
      "title": "Queue Depth",
      "type": "gauge",
      "targets": [
        {
          "expr": "selfmx_emails_queued"
        }
      ]
    }
  ]
}
```

## Alerting

### Prometheus Alerting Rules

Create `alerts.yml`:

```yaml
groups:
  - name: selfmx
    rules:
      - alert: HighQueueDepth
        expr: selfmx_emails_queued > 100
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Email queue is backing up"
          description: "Queue depth is {{ $value }} (threshold: 100)"

      - alert: HighBounceRate
        expr: |
          rate(selfmx_emails_bounced[5m])
          / rate(selfmx_emails_total[5m]) > 0.1
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High email bounce rate"
          description: "Bounce rate is {{ $value | humanizePercentage }}"

      - alert: SMTPConnectionFailure
        expr: selfmx_smtp_connections == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "No SMTP connections"
          description: "Unable to connect to SMTP server"

      - alert: HighAPILatency
        expr: |
          histogram_quantile(0.95,
            rate(selfmx_api_request_duration_bucket[5m])
          ) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High API latency"
          description: "P95 latency is {{ $value }}s"
```

## Log Aggregation

### Structured Logging

SelfMX outputs JSON logs for easy parsing:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "info",
  "message": "Email delivered",
  "email_id": "msg_abc123",
  "to": "recipient@example.com",
  "duration_ms": 245
}
```

### Loki Configuration

Send logs to Grafana Loki:

```yaml
# Docker Compose addition
services:
  selfmx:
    logging:
      driver: loki
      options:
        loki-url: "http://loki:3100/loki/api/v1/push"
        loki-batch-size: "100"
```

### Log Queries

Useful LogQL queries:

```
# All errors
{app="selfmx"} |= "error"

# Bounced emails
{app="selfmx"} | json | event="bounced"

# Slow API requests
{app="selfmx"} | json | duration_ms > 1000
```

## Health Checks

### Endpoint

```bash
curl http://localhost:8080/health
```

Response:

```json
{
  "status": "ok",
  "database": "connected",
  "smtp": "connected",
  "queue_depth": 5,
  "version": "1.0.0"
}
```

### Docker Health Check

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 10s
```

### Kubernetes Probes

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

## Recommended Thresholds

| Metric               | Warning      | Critical     |
| -------------------- | ------------ | ------------ |
| Queue Depth          | > 100        | > 500        |
| Bounce Rate          | > 5%         | > 10%        |
| API Latency (P95)    | > 500ms      | > 2s         |
| Error Rate           | > 1%         | > 5%         |
| SMTP Connection Time | > 5s         | > 30s        |

## Next Steps

- [Troubleshooting](/guides/troubleshooting) - Debug common issues
- [Architecture](/concepts/architecture) - Understand the system
