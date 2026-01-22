---
layout: ../../layouts/DocsLayout.astro
title: Deployment
description: Deploy SelfMX to production environments.
---

This guide covers deploying SelfMX to various production environments.

## Docker Compose (Recommended)

The simplest production deployment uses Docker Compose.

### Production Configuration

Create `docker-compose.yml`:

```yaml
version: "3.8"

services:
  selfmx:
    image: ghcr.io/aduggleby/selfmx:latest
    restart: always
    ports:
      - "8080:8080"
    environment:
      - DATABASE_URL=postgres://selfmx:${DB_PASSWORD}@db:5432/selfmx
      - API_KEY=${API_KEY}
      - SMTP_HOST=${SMTP_HOST}
      - SMTP_PORT=${SMTP_PORT}
      - SMTP_USER=${SMTP_USER}
      - SMTP_PASS=${SMTP_PASS}
      - LOG_LEVEL=info
      - QUEUE_WORKERS=4
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  db:
    image: postgres:15-alpine
    restart: always
    environment:
      - POSTGRES_USER=selfmx
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=selfmx
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U selfmx"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  pgdata:
```

### Environment File

Create `.env`:

```bash
DB_PASSWORD=your-secure-database-password
API_KEY=your-secure-api-key
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USER=apikey
SMTP_PASS=your-smtp-password
```

### Deploy

```bash
docker compose up -d
```

## Reverse Proxy with HTTPS

### Caddy (Recommended)

Add Caddy to your docker-compose.yml:

```yaml
services:
  caddy:
    image: caddy:2-alpine
    restart: always
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
    depends_on:
      - selfmx

volumes:
  caddy_data:
```

Create `Caddyfile`:

```
api.yourdomain.com {
    reverse_proxy selfmx:8080
}
```

### Nginx

```nginx
server {
    listen 443 ssl http2;
    server_name api.yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/api.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.yourdomain.com/privkey.pem;

    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## Kubernetes

### Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: selfmx
spec:
  replicas: 3
  selector:
    matchLabels:
      app: selfmx
  template:
    metadata:
      labels:
        app: selfmx
    spec:
      containers:
        - name: selfmx
          image: ghcr.io/aduggleby/selfmx:latest
          ports:
            - containerPort: 8080
          envFrom:
            - secretRef:
                name: selfmx-secrets
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
          resources:
            requests:
              memory: "256Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "500m"
```

### Service

```yaml
apiVersion: v1
kind: Service
metadata:
  name: selfmx
spec:
  selector:
    app: selfmx
  ports:
    - port: 80
      targetPort: 8080
  type: ClusterIP
```

### Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: selfmx-secrets
type: Opaque
stringData:
  DATABASE_URL: postgres://user:pass@postgres:5432/selfmx
  API_KEY: your-api-key
  SMTP_HOST: smtp.example.com
  SMTP_PORT: "587"
  SMTP_USER: apikey
  SMTP_PASS: your-smtp-password
```

## Cloud Platforms

### AWS ECS

Use the Fargate launch type for serverless container deployment:

1. Create ECR repository or use GHCR image
2. Create ECS cluster with Fargate
3. Create task definition with container settings
4. Create service with load balancer
5. Configure RDS PostgreSQL

### Google Cloud Run

```bash
gcloud run deploy selfmx \
  --image ghcr.io/aduggleby/selfmx:latest \
  --platform managed \
  --region us-central1 \
  --set-env-vars "DATABASE_URL=..." \
  --set-env-vars "API_KEY=..." \
  --set-env-vars "SMTP_HOST=..." \
  --allow-unauthenticated
```

### DigitalOcean App Platform

Create `app.yaml`:

```yaml
name: selfmx
services:
  - name: api
    image:
      registry_type: GHCR
      registry: aduggleby
      repository: selfmx
      tag: latest
    instance_size_slug: basic-xxs
    instance_count: 1
    envs:
      - key: DATABASE_URL
        scope: RUN_TIME
        value: ${db.DATABASE_URL}
      - key: API_KEY
        scope: RUN_TIME
        type: SECRET

databases:
  - name: db
    engine: PG
    version: "15"
```

## Security Checklist

Before going to production:

- [ ] Use HTTPS with valid SSL certificate
- [ ] Set strong, unique API key
- [ ] Use strong database password
- [ ] Configure firewall rules
- [ ] Enable database backups
- [ ] Set up monitoring and alerting
- [ ] Review rate limit settings
- [ ] Test SMTP connectivity

## Next Steps

- [Monitoring](/guides/monitoring) - Set up observability
- [Troubleshooting](/guides/troubleshooting) - Common issues
