# TMB — Sistema de Gestão de Pedidos

Desafio técnico: API .NET + worker assíncrono (RabbitMQ) + frontend React + PostgreSQL.

> 🚧 Em desenvolvimento. Instruções completas de execução, decisões técnicas e diagramas serão adicionados ao final.

## Estrutura

```
src/
  Orders.Domain/          Entidades e regras de transição de status
  Orders.Contracts/       Contratos de mensagens (OrderCreated, OrderStatusChanged)
  Orders.Infrastructure/  EF Core, migrations, MassTransit/Outbox, OpenTelemetry
  Orders.Api/             API REST, SignalR, health checks, módulo IA
  Orders.Worker/          Consumidor que processa os pedidos
tests/
  Orders.UnitTests/
  Orders.IntegrationTests/
```

## Pré-requisitos

- .NET SDK 10
- Node.js 20+
- Docker Desktop

## Build local

```bash
dotnet build Orders.slnx
dotnet test Orders.slnx
```
