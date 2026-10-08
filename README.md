# TMB · Sistema de Gestão de Pedidos

[![CI](https://github.com/vitordanirl/TMB/actions/workflows/ci.yml/badge.svg)](https://github.com/vitordanirl/TMB/actions/workflows/ci.yml)

Sistema para criar, listar e acompanhar pedidos. Cada pedido criado é publicado no RabbitMQ e processado de forma assíncrona por um worker idempotente (**Pendente → Processando → Finalizado**), com o status atualizado em tempo real no frontend. Inclui ainda tracing ponta a ponta e perguntas em linguagem natural sobre os pedidos, respondidas por IA com dados reais.

**Stack:** .NET 10 · ASP.NET Core · EF Core · PostgreSQL 17 · RabbitMQ 4 + MassTransit · React 19 + Vite + TypeScript · SignalR · OpenTelemetry + Jaeger · Claude (Anthropic) · Docker Compose

- [Como rodar](#como-rodar)
- [O que foi entregue](#o-que-foi-entregue)
- [Arquitetura](#arquitetura)
- [Decisões técnicas](#decisões-técnicas)
- [API](#api)
- [Testes](#testes)
- [Desenvolvimento local](#desenvolvimento-local)
- [Variáveis de ambiente](#variáveis-de-ambiente)
- [Solução de problemas](#solução-de-problemas)
- [Limitações e próximos passos](#limitações-e-próximos-passos)

## Como rodar

Pré-requisito: **Docker Desktop** (ou Docker Engine + Compose v2). Um único comando sobe tudo:

```bash
docker compose --env-file .env.example up -d --build
```

Para personalizar portas e credenciais, copie `.env.example` para `.env`, ajuste e rode `docker compose up -d --build`. Na primeira execução o build das imagens leva alguns minutos.

| Serviço | URL | |
|---|---|---|
| **Frontend** | http://localhost:3000 | pedidos, criação, detalhes e IA |
| API | http://localhost:8080 | REST, SignalR, health checks |
| Documentação da API | http://localhost:8080/docs | Scalar; OpenAPI em `/openapi/v1.json` |
| Jaeger | http://localhost:16686 | traces de `orders-api` e `orders-worker` |
| RabbitMQ | http://localhost:15672 | usuário/senha `RABBITMQ_USER` / `RABBITMQ_PASSWORD` |
| pgAdmin | http://localhost:5050 | sem login; servidor "Orders" já registrado |
| PostgreSQL | `localhost:5432` | credenciais `POSTGRES_*` |

A ordem de subida é garantida por healthchecks: `postgres` + `rabbitmq` → `migrator` (aplica as migrations e encerra) → `api` + `worker` → `web`. Para validar o ambiente de ponta a ponta:

```bash
./scripts/smoke-test.sh
```

```bash
docker compose ps                       # status e saúde dos serviços
docker compose logs -f worker           # acompanhar o processamento
docker compose down                     # parar e remover (adicione -v para apagar os dados)
```

### Perguntas sobre os pedidos (IA, opcional)

Em http://localhost:3000/assistente, perguntas como *"Quantos pedidos estão pendentes?"* ou *"Qual o tempo médio para aprovar os pedidos?"* são respondidas com dados reais. Para habilitar, defina a chave no `.env` e recrie a API:

```bash
ANTHROPIC_API_KEY=sk-ant-...      # no .env
docker compose up -d api
```

Sem a chave o módulo fica desativado e a tela explica como configurar.

## O que foi entregue

### Requisitos

| Requisito | Implementação |
|---|---|
| `POST /orders`, `GET /orders`, `GET /orders/{id}` | Minimal APIs com validação, paginação, filtro por status e ProblemDetails (RFC 9457) |
| Atributos `id`, `cliente`, `produto`, `valor`, `status`, `data_criacao` | Mesmos nomes no banco e no JSON (snake_case) |
| Persistir e publicar ao criar | Pedido e mensagem gravados na **mesma transação** (Transactional Outbox) |
| Worker: Processando e, após 5 s, Finalizado | Dois consumidores, cada etapa atômica; atraso configurável |
| Sequência obrigatória de status | Regra no domínio (`Order.TransitionTo`) que rejeita saltos e retrocessos |
| Consumidor idempotente | Inbox (MessageId) + guarda de estado + concorrência otimista (`xmin`) |
| `CorrelationId = OrderId` e `EventType = OrderCreated` | Filtros de publish/send aplicados a toda mensagem de pedido |
| Health checks de API, banco e mensageria | `/health/live` e `/health/ready` (PostgreSQL + RabbitMQ) na API e no worker |
| Frontend: tabela responsiva, formulário, detalhes, feedback de status | React com tabela/cards, validação, linha do tempo, badge animado e notificações |
| Ambiente orquestrado com um comando | Docker Compose com API, worker, frontend, banco, pgAdmin, RabbitMQ e Jaeger |
| Variáveis sensíveis fora do código | Somente via variáveis de ambiente; `.env.example` versionado |
| Migrations automáticas | Serviço `migrator` (EF Core bundle) antes da API e do worker |
| Healthchecks na orquestração | Em todos os serviços, com `depends_on` condicionado |

### Diferenciais

| Diferencial | Implementação |
|---|---|
| Outbox Pattern | Transactional Outbox/Inbox do MassTransit sobre EF Core/PostgreSQL |
| Histórico de status | Tabela `order_status_history`, exibida como linha do tempo |
| Status em tempo real com fallback | SignalR com reconexão; polling automático enquanto sem conexão |
| Testes de integração com dependências reais | Testcontainers (PostgreSQL e RabbitMQ descartáveis) |
| Tracing ponta a ponta | OpenTelemetry → Jaeger: um trace do `POST` até o "Finalizado", incluindo SQL |
| Golden tests | Verify: OpenAPI, respostas, erros, envelope das mensagens e requisição à IA |
| Módulo IA/Analytics | Claude com ferramentas somente leitura sobre os dados reais |
| Diagramas de arquitetura | [docs/architecture.md](docs/architecture.md) |

## Arquitetura

```mermaid
flowchart LR
    user(["Navegador"]) --> web["web<br/>nginx + React"]
    web -- "/api · /hubs" --> api["api<br/>ASP.NET Core"]
    api -- "pedido + outbox<br/>(1 transação)" --> db[("PostgreSQL")]
    api -- "OrderCreated" --> mq{{"RabbitMQ"}}
    mq --> worker["worker<br/>consumidores idempotentes"]
    worker -- "status + histórico + outbox" --> db
    worker -- "OrderStatusChanged" --> mq
    mq -- "notificações" --> api
    api -. "SignalR" .-> web
    api -- "ferramentas<br/>somente leitura" --> claude["Claude"]
    api & worker -. "OTLP" .-> jaeger["Jaeger"]
```

Detalhes, com diagramas de sequência, modelo de dados, garantias de mensageria e organização do código: **[docs/architecture.md](docs/architecture.md)**.

## Decisões técnicas

| Escolha | Por quê |
|---|---|
| **.NET 10 + Minimal APIs** | Versão LTS atual; endpoints enxutos com validação nativa (`AddValidation`), ProblemDetails e OpenAPI integrados. |
| **EF Core + Npgsql** | Migrations versionadas, LINQ tipado e integração direta com o Outbox do MassTransit (mesma transação). Concorrência otimista sem coluna extra, via `xmin` do PostgreSQL. |
| **PostgreSQL 17** | Exigido pelo desafio; transações robustas para o Outbox/Inbox e agregações para a IA. |
| **RabbitMQ + MassTransit 8.5** | RabbitMQ é o broker mais comum em .NET, leve para rodar local e com painel de administração. O MassTransit entrega Outbox/Inbox, retry, DLQ e tracing prontos. A linha 8.x é Apache 2.0; a v9 é comercial. A telemetria de uso do MassTransit está desativada. |
| **Outbox + Inbox** | Elimina as duas falhas clássicas (pedido salvo sem mensagem e mensagem processada duas vezes) sem transações distribuídas. |
| **Worker em duas mensagens** | Cada etapa (Processando, Finalizado) é uma transação própria, então o estado intermediário fica visível e uma queda no meio é retomada pela reentrega. |
| **React 19 + Vite + TypeScript** | SPA rápida e tipada. TanStack Query para cache e polling, React Hook Form + Zod para formulários e Tailwind CSS 4 para a interface responsiva com modo escuro. |
| **SignalR + fallback** | Nativo do ASP.NET Core, com WebSocket e reconexão automática. A fila de notificações é por instância da API (fan-out), e o polling cobre a falta de conexão. |
| **nginx na frente da SPA** | Mesma origem para `/api` e `/hubs` (sem CORS), cache de assets e falha rápida se a API cair. |
| **OpenTelemetry + Jaeger** | Padrão aberto. O contexto W3C viaja nos headers das mensagens, o que dá um trace contínuo entre API, broker e worker. |
| **Testcontainers + Verify** | Testes com PostgreSQL e RabbitMQ reais em containers descartáveis. Os golden tests detectam qualquer mudança de contrato (API, OpenAPI e mensagens). |
| **Claude (Anthropic) com tool use** | A IA escolhe ferramentas tipadas e validadas em vez de gerar SQL, o que é mais seguro e dá respostas baseadas em dados reais. Modelo `claude-opus-5-5`, configurável. |
| **Docker Compose** | Um comando sobe o ambiente completo. Imagens Alpine multi-stage, executadas como usuário não-root. |

## API

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/orders` | Cria um pedido (`201` + `Location`) |
| `GET` | `/orders?status=&page=&page_size=` | Lista paginada, mais recentes primeiro |
| `GET` | `/orders/{id}` | Detalhes com histórico de status |
| `GET` | `/health/live` · `/health/ready` | Processo · PostgreSQL e RabbitMQ |
| `GET` | `/ai/status` | Se o módulo de IA está habilitado |
| `POST` | `/ai/ask` | Pergunta em linguagem natural sobre os pedidos |
| `WS` | `/hubs/orders` | Eventos `OrderCreated` e `OrderStatusChanged` (SignalR) |

```bash
curl -X POST http://localhost:8080/orders -H "Content-Type: application/json" \
  -d '{"cliente":"Maria Silva","produto":"Notebook","valor":4599.90}'
```

```json
{
  "id": "01a11965-7ea4-729b-b590-4f3f5fc49019",
  "cliente": "Maria Silva",
  "produto": "Notebook",
  "valor": 4599.9,
  "status": "Pendente",
  "data_criacao": "2026-10-08T02:44:04.388236+00:00",
  "data_atualizacao": "2026-10-08T02:44:04.388236+00:00"
}
```

Erros seguem RFC 9457 (ProblemDetails), com mensagens em português e chaves no formato do contrato:

```json
{
  "title": "Um ou mais campos são inválidos.",
  "status": 400,
  "errors": { "cliente": ["Campo obrigatório."], "valor": ["Deve ser maior que zero e ter no máximo 16 dígitos inteiros."] }
}
```

A documentação interativa completa fica em http://localhost:8080/docs.

## Testes

```bash
dotnet test Orders.slnx           # requer Docker (Testcontainers)
cd web && npm test
```

| Suíte | O que cobre |
|---|---|
| **Unitários** (`tests/Orders.UnitTests`) | Regras de domínio (criação, sequência de status) e observabilidade (nomeação de spans, descarte de spans órfãos) |
| **Integração** (`tests/Orders.IntegrationTests`) | API e worker reais contra PostgreSQL e RabbitMQ em containers descartáveis. Cobrem o ciclo completo, `CorrelationId`/`EventType` inspecionados no broker, mesma mensagem 2x, duplicata para pedido finalizado, 5 duplicatas concorrentes, health checks e o módulo de IA com modelo simulado no nível HTTP |
| **Golden** (Verify) | Documento OpenAPI, respostas, erros 400/404, envelope do `OrderCreated` e requisição enviada à IA (snapshots em `tests/Orders.IntegrationTests/Snapshots`) |
| **Frontend** (Vitest + Testing Library) | Formulário, linha do tempo, badge, cache em tempo real, política de polling, cliente HTTP e tela de IA |
| **Ponta a ponta** (`scripts/smoke-test.sh`) | Ambiente do Compose completo: serviços saudáveis, pedido criado pelo frontend até "Finalizado" |

Se um contrato mudar intencionalmente, o golden test falha e gera um `*.received.txt`. Depois de revisar, renomeie-o para `*.verified.txt`.

A **CI** (GitHub Actions) roda build com warnings como erro, todos os testes .NET, lint/tipos/testes/build do frontend e o teste de ponta a ponta com `docker compose`.

## Desenvolvimento local

Requer .NET SDK 10 e Node.js 20+. Suba só a infraestrutura com o Compose e rode as aplicações pelo SDK:

```bash
docker compose --env-file .env.example up -d postgres rabbitmq migrator jaeger

export ConnectionStrings__Orders="Host=localhost;Port=5432;Database=orders;Username=orders;Password=orders_dev_password"
export ConnectionStrings__RabbitMq="amqp://orders:orders_dev_password@localhost:5672/"
dotnet run --project src/Orders.Api       # http://localhost:5080
dotnet run --project src/Orders.Worker    # http://localhost:5090 (health checks)
```

```bash
cd web
npm ci
VITE_API_PROXY_TARGET=http://localhost:5080 npm run dev   # http://localhost:5173
npm run lint && npm run typecheck && npm test
```

Nova migration: `dotnet ef migrations add <Nome> -p src/Orders.Infrastructure -s src/Orders.Infrastructure -o Persistence/Migrations` (o `dotnet-ef` é ferramenta local: `dotnet tool restore`).

## Variáveis de ambiente

Definidas em `.env` (modelo em [`.env.example`](.env.example)). O Compose falha com mensagem clara se faltar alguma obrigatória.

| Variável | Padrão no exemplo | Uso |
|---|---|---|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | `orders` / `orders` / `orders_dev_password` | Banco de dados |
| `RABBITMQ_USER` / `RABBITMQ_PASSWORD` | `orders` / `orders_dev_password` | Broker (usados numa URI AMQP: evite caracteres especiais) |
| `PGADMIN_EMAIL` / `PGADMIN_PASSWORD` | `admin@example.com` / `admin_dev_password` | pgAdmin |
| `ANTHROPIC_API_KEY` | vazio | Habilita o módulo de IA |
| `AI_MODEL` / `AI_EFFORT` | `claude-opus-5-5` / `low` | Modelo e esforço de raciocínio da IA |
| `ORDER_COMPLETION_DELAY` | `00:00:05` | Tempo entre Processando e Finalizado |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Ambiente das aplicações .NET |
| `API_PORT`, `WEB_PORT`, `POSTGRES_PORT`, `RABBITMQ_PORT`, `RABBITMQ_MANAGEMENT_PORT`, `PGADMIN_PORT`, `JAEGER_UI_PORT` | `8080`, `3000`, `5432`, `5672`, `15672`, `5050`, `16686` | Portas expostas no host |

## Solução de problemas

| Sintoma | Causa provável e solução |
|---|---|
| `required variable ... is missing a value` | Falta o `.env`. Use `--env-file .env.example` ou copie o arquivo de exemplo. |
| `port is already allocated` | Porta em uso no host (ex.: um PostgreSQL local na 5432). Altere a porta correspondente no `.env`. |
| Indicador "Atualização periódica" no frontend | A conexão SignalR não está ativa; a tela segue atualizando por polling e reconecta sozinha. Veja `docker compose logs api`. |
| Pedidos ficam em "Pendente" | Worker fora do ar: `docker compose ps worker` e `docker compose logs worker`. Mensagens com falha ficam nas filas `*_error` do RabbitMQ. |
| `dotnet test` falha ao iniciar containers | Os testes de integração precisam do Docker em execução. |
| Golden test falhou | O contrato mudou. Compare o `*.received.txt` com o `*.verified.txt` e aprove se a mudança for intencional. |
| `/ai/ask` retorna 503 | `ANTHROPIC_API_KEY` não definida para o serviço `api`. |
| `npm install` falha com `Cannot read properties of null (reading 'edgesOut')` | Bug do npm 10.2 (Node 20.11). Atualize o Node/npm ou use `npx npm@11 install`; `npm ci` funciona normalmente. |

## Limitações e próximos passos

- **Autenticação/autorização:** fora do escopo do desafio; a API é aberta.
- **Espera de 5 s no consumidor:** ocupa um slot do consumidor durante a espera. Com volume alto, delegaria a um agendador (delayed exchange do RabbitMQ ou Quartz).
- **Tracing no navegador:** os traces começam na API. Instrumentar o frontend exigiria o SDK web do OpenTelemetry e CORS no coletor.
- **Jaeger em memória e pgAdmin sem login:** configurações de desenvolvimento.
- **IA:** não guarda contexto entre perguntas (cada pergunta é independente) e o rate limit é em memória (por instância).
