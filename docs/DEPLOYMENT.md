# Deployment

## Environments

| Environment | Stack | Purpose |
|-------------|-------|---------|
| Local dev | docker-compose | Single developer, all services on one machine |
| On-premise | docker-compose or Kubernetes | Self-hosted production or staging |
| Cloud | Kubernetes (AKS / EKS / GKE) + managed services | Scalable, highly available production |

---

## Local Development (docker-compose)

```yaml
# deploy/docker-compose.yml
services:
  postgres:
    image: timescale/timescaledb:latest-pg16
    environment:
      POSTGRES_DB: trader
      POSTGRES_USER: trader
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${REDIS_PASSWORD}
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: trader
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    ports:
      - "5672:5672"
      - "15672:15672"  # management UI

  api:
    build: ../src/Trader.Api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Host=postgres;Database=trader;Username=trader;Password=${DB_PASSWORD}
      - Redis__Connection=redis:6379,password=${REDIS_PASSWORD}
      - RabbitMQ__Host=rabbitmq
    ports:
      - "7220:8080"
    depends_on: [postgres, redis, rabbitmq]

volumes:
  pgdata:
```

```bash
# Start all services
cp deploy/.env.example deploy/.env   # fill in passwords
docker-compose -f deploy/docker-compose.yml up -d

# Run API locally (connects to dockerized infra)
cd src/Trader.Api && dotnet run

# Run frontend
cd frontend && npm run dev
```

---

## On-Premise (Kubernetes)

For single-server or small cluster deployment without a cloud provider.

```
deploy/k8s/
├── namespace.yaml
├── postgres/          StatefulSet + PVC + Service
├── redis/             Deployment + Service
├── rabbitmq/          StatefulSet + Service
├── api/               Deployment + Service + Ingress
└── secrets/           (use Sealed Secrets or SOPS for encryption)
```

```bash
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -f deploy/k8s/secrets/      # apply encrypted secrets first
kubectl apply -f deploy/k8s/postgres/
kubectl apply -f deploy/k8s/redis/
kubectl apply -f deploy/k8s/rabbitmq/
kubectl apply -f deploy/k8s/api/
```

Reverse proxy recommendation: **nginx Ingress Controller** with cert-manager for automatic TLS.

---

## Cloud (Azure — reference)

Swap managed services for self-hosted ones to improve reliability and reduce ops burden.

| Self-Hosted | Azure Managed | AWS Managed |
|-------------|---------------|-------------|
| PostgreSQL | Azure Database for PostgreSQL Flexible Server | Amazon RDS for PostgreSQL |
| Redis | Azure Cache for Redis | ElastiCache |
| RabbitMQ | Azure Service Bus | Amazon SQS/SNS |
| Container | AKS (Kubernetes) | EKS |
| Secrets | Azure Key Vault | AWS Secrets Manager |
| Logs/Traces | Azure Monitor / Grafana | CloudWatch / X-Ray |

```bash
# Example: provision AKS + Azure Container Registry
az group create -n trader-rg -l eastus
az acr create -n traderregistry -g trader-rg --sku Basic
az aks create -n trader-aks -g trader-rg --attach-acr traderregistry

# Build and push
docker build -t traderregistry.azurecr.io/trader-api:latest src/Trader.Api
docker push traderregistry.azurecr.io/trader-api:latest
```

MassTransit config for Azure Service Bus (provider swap — no agent code changes):
```json
{
  "MassTransit": {
    "Transport": "AzureServiceBus",
    "AzureServiceBus": {
      "ConnectionString": ""
    }
  }
}
```

---

## CI/CD Pipeline (GitHub Actions skeleton)

```yaml
# .github/workflows/ci.yml
jobs:
  build-test:
    steps:
      - dotnet build
      - dotnet test --configuration Release
      - dotnet list package --vulnerable --include-transitive   # security gate
  
  build-image:
    needs: build-test
    steps:
      - docker build + push to registry
  
  deploy-staging:
    needs: build-image
    steps:
      - kubectl set image deployment/trader-api ...
  
  deploy-production:
    needs: deploy-staging
    environment: production     # requires manual approval
    steps:
      - kubectl set image deployment/trader-api ...
```

---

## Observability Stack

```yaml
# Add to docker-compose.yml for local observability
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest

  prometheus:
    image: prom/prometheus:latest

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3001:3000"

  tempo:
    image: grafana/tempo:latest
```

The API is instrumented with `OpenTelemetry.Extensions.Hosting`. All agent events carry a `TraceId`.

---

## Configuration per Environment

Use `.NET` layered configuration:
1. `appsettings.json` — defaults (no secrets)
2. `appsettings.{Environment}.json` — env-specific overrides
3. Environment variables — secrets (highest priority)

```bash
# Example env vars in production
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=...
Providers__Quote=binance
Binance__ApiKey=...
Binance__SecretKey=...
Jwt__Secret=...
```
