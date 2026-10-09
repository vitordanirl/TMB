#!/usr/bin/env bash
# Teste de fumaça ponta a ponta contra o ambiente do docker compose já em execução:
# espera os serviços ficarem saudáveis, cria um pedido pelo frontend (nginx -> API) e
# acompanha até "Finalizado", conferindo o histórico Pendente -> Processando -> Finalizado.
#
# Uso: docker compose --env-file .env.example up -d --build && ./scripts/smoke-test.sh
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:3000}"
SERVICES=(postgres rabbitmq api worker web jaeger pgadmin)
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-180}"

log() { printf '[smoke] %s\n' "$*"; }
fail() { printf '[smoke] FALHA: %s\n' "$*" >&2; exit 1; }

compose() { docker compose --env-file "${ENV_FILE:-.env.example}" "$@"; }

log "Aguardando serviços saudáveis: ${SERVICES[*]}"
deadline=$((SECONDS + TIMEOUT_SECONDS))
for service in "${SERVICES[@]}"; do
  until [ "$(docker inspect -f '{{.State.Health.Status}}' "$(compose ps -q "$service")" 2>/dev/null)" = "healthy" ]; do
    [ "$SECONDS" -lt "$deadline" ] || fail "serviço '$service' não ficou saudável em ${TIMEOUT_SECONDS}s"
    sleep 2
  done
  log "  $service: healthy"
done

migrator_exit="$(docker inspect -f '{{.State.ExitCode}}' "$(compose ps -aq migrator)")"
[ "$migrator_exit" = "0" ] || fail "migrator terminou com código $migrator_exit"
log "  migrator: migrations aplicadas"

log "Criando pedido via ${BASE_URL}/api/orders"
response="$(curl -fsS -X POST "${BASE_URL}/api/orders" -H 'content-type: application/json' \
  -d '{"cliente":"Smoke Test","produto":"CI","valor":42.5}')"
order_id="$(printf '%s' "$response" | grep -oE '"id":"[0-9a-f-]{36}"' | cut -d'"' -f4)"
[ -n "$order_id" ] || fail "resposta inesperada: $response"
log "  pedido $order_id criado"

log "Acompanhando o processamento assíncrono"
deadline=$((SECONDS + 60))
until details="$(curl -fsS "${BASE_URL}/api/orders/${order_id}")" && printf '%s' "$details" | grep -q '"status":"Finalizado"'; do
  [ "$SECONDS" -lt "$deadline" ] || fail "pedido não foi finalizado em 60s: $details"
  sleep 1
done

history="$(printf '%s' "$details" | grep -oE '"status_novo":"[A-Za-z]+"' | cut -d'"' -f4 | paste -sd' ' -)"
[ "$history" = "Pendente Processando Finalizado" ] || fail "histórico inesperado: $history"
log "  histórico: $history"

log "Conferindo health checks e documentação"
curl -fsS "${BASE_URL}/api/health/ready" | grep -q '"status":"Healthy"' || fail "/health/ready não está Healthy"
curl -fsS -o /dev/null "${BASE_URL}/api/openapi/v1.json" || fail "documento OpenAPI indisponível"

log "OK: fluxo completo validado"
