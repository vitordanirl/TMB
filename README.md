# TMB — Sistema de Gestão de Pedidos

Desafio técnico: API .NET + worker assíncrono (RabbitMQ) + frontend React + PostgreSQL.

> 🚧 Em desenvolvimento. Decisões técnicas, diagramas e detalhes serão completados ao final.

## Como rodar

Pré-requisito: Docker Desktop (ou Docker Engine + Compose v2).

```bash
docker compose --env-file .env.example up -d --build
```

Para personalizar portas/credenciais, copie `cp .env.example .env`, ajuste e rode `docker compose up -d --build`.

| Serviço | URL | Observação |
|---|---|---|
| **Frontend** | http://localhost:3000 | lista, criação e detalhes dos pedidos (status em tempo real) |
| API | http://localhost:8080 | `/orders`, `/hubs/orders` (SignalR), `/health/live`, `/health/ready` |
| Documentação da API | http://localhost:8080/docs | Scalar (OpenAPI em `/openapi/v1.json`) |
| Jaeger (tracing) | http://localhost:16686 | serviços `orders-api` e `orders-worker`; filtre por tag `order.id=<id>` |
| RabbitMQ Management | http://localhost:15672 | usuário/senha: `RABBITMQ_USER` / `RABBITMQ_PASSWORD` |
| pgAdmin | http://localhost:5050 | sem login; servidor "Orders" já registrado |
| PostgreSQL | localhost:5432 | credenciais `POSTGRES_*` do `.env` |

Um único trace cobre o fluxo inteiro de um pedido: `POST /orders` → Outbox → RabbitMQ → worker (Processando) → worker (Finalizado) → notificações SignalR, incluindo os comandos SQL. Os logs incluem `TraceId`, permitindo ir do log ao trace.

Ordem de subida (garantida por healthchecks): `postgres` + `rabbitmq` → `migrator` (aplica as migrations e encerra) → `api` + `worker` → `web`.

```bash
docker compose ps          # status/saúde dos serviços
docker compose logs -f worker
docker compose down        # para tudo (use -v para apagar os dados)
```

## Estrutura

```
src/
  Orders.Domain/          Entidades e regras de transição de status
  Orders.Contracts/       Contratos de mensagens (OrderCreated, OrderStatusChanged...)
  Orders.Infrastructure/  EF Core, migrations, MassTransit/Outbox, health checks
  Orders.Api/             API REST
  Orders.Worker/          Consumidores que processam os pedidos
web/                      Frontend React + Vite + TypeScript (servido por nginx)
tests/
  Orders.UnitTests/
  Orders.IntegrationTests/
Dockerfile                Multi-stage com targets api, worker e migrator
docker-compose.yml        Orquestração completa
```

## Desenvolvimento local (sem Docker para as apps)

Requer .NET SDK 10. Suba só a infraestrutura e rode as apps pelo SDK:

```bash
docker compose --env-file .env.example up -d postgres rabbitmq migrator
dotnet build Orders.slnx
dotnet test Orders.slnx
```

As apps leem `ConnectionStrings__Orders` e `ConnectionStrings__RabbitMq` de variáveis de ambiente.

### Testes

```bash
dotnet test Orders.slnx
```

- **Unitários** (`tests/Orders.UnitTests`): regras de domínio e observabilidade.
- **Integração** (`tests/Orders.IntegrationTests`): sobem PostgreSQL e RabbitMQ reais em containers descartáveis (Testcontainers — requer Docker) e rodam API e Worker em processo. Cobrem o ciclo completo do pedido, idempotência (mesma mensagem 2x e duplicatas concorrentes), metadados das mensagens e health checks.
- **Golden tests** (Verify): respostas da API, documento OpenAPI e envelope das mensagens comparados com snapshots aprovados em `tests/Orders.IntegrationTests/Snapshots`. Se o contrato mudar intencionalmente, revise o `*.received.txt` gerado e renomeie-o para `*.verified.txt`.



### Frontend

Requer Node.js 20+. Com a API rodando em `localhost:8080` (via compose ou SDK):

```bash
cd web
npm ci
npm run dev        # http://localhost:5173 (proxy de /api e /hubs para a API)
npm test           # testes (Vitest + Testing Library)
npm run lint && npm run typecheck
```
