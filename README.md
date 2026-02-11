# Desafio Técnico PagueVeloz: Sistema de Transações Financeiras

Este repositório contém a implementação do desafio técnico para o sistema de processamento de transações financeiras da PagueVeloz. O projeto foi construído focando em **DDD (Domain-Driven Design)**, alta coesão, baixo acoplamento e resiliência.

---

## Arquitetura e Decisões Técnicas

A solução adota uma abordagem de **DDD (Domain-Driven Design)**, dividindo a aplicação em camadas concêntricas para isolar o domínio das regras de negócio de detalhes de infraestrutura (banco de dados, frameworks, UI).

### Estrutura do Projeto
- **PagueVeloz.Domain**: O núcleo do domínio. Contém as entidades (`Account`, `Transaction`), objetos de valor, interfaces de repositório e regras de negócio puras. Não depende de nenhum framework externo.
- **PagueVeloz.Application**: Camada de aplicação que orquestra os casos de uso (Commands/Queries). Responsável pela "dança" entre as entidades e os repositórios.
- **PagueVeloz.Infrastructure**: Implementação dos detalhes técnicos. Aqui residem o `DbContext` (EF Core), configurações de banco de dados, implementações de repositórios e integrações externas.
- **PagueVeloz.API**: A camada de apresentação (Entrypoint), expondo os endpoints REST e gerenciando a injeção de dependência.

### Justificativas Técnicas
- **Serilog**: Biblioteca de logging estruturado, essencial para observabilidade em ambientes distribuídos, permitindo buscas ricas em logs (ex: filtrar por `TransactionId`).
- **Polly**: Biblioteca de resiliência utilizada para implementar políticas de **Retry** (tentar novamente) e **Circuit Breaker** (interromper chamadas falhas) em operações críticas ou instáveis.
- **Grafana**: Plataforma de observabilidade que permite visualizar métricas de negócio e performance.
- **InfluxDB**: Banco de dados de métricas que permite armazenar e visualizar métricas de negócio e performance.
- **K6**: Ferramenta de testes de carga que permite validar a performance sob estresse.
- **Mapperly**: Alternativa de mais alto desempenho ao AutoMapper, compatível com CQRS, Saga e com Result pattern nativo.
- **MassTransit**: Framework de orquestração de mensageria. Utilizado para implementar o **Saga Pattern** na operação de transfer, garantindo consistência eventual com compensação automática em caso de falha.
- **OpenTelemetry + Prometheus**: Pipeline de métricas via OpenTelemetry SDK, exportando contadores e histogramas para coleta Prometheus no endpoint `/metrics`.
- **In Memory**: Utilizado em memória Caching, mensageria e concorrência, respeitando o princípio de inversão de dependência, podendo ser facilmente trocado para uma nova estratégia no futuro.
- **FluentValidator**: Biblioteca de validação extensível, verificando regras de negócio rapidamente antes de propagar para regiões mais críticas do código.

### Saga Pattern — Transfer

A operação de **transfer** utiliza o **Saga Pattern** (orquestrado via MassTransit com state machine) para garantir atomicidade entre contas distintas:

1. `TransferRequested` — Saga iniciada, debita a conta origem.
2. `DebitSourceCompleted` — Débito confirmado, credita a conta destino.
3. `CreditDestinationCompleted` — Crédito confirmado, transfer concluído.
4. **Compensação**: Se o crédito no destino falha, a saga reverte automaticamente o débito na origem.

Essa abordagem garante consistência eventual sem acoplamento direto entre contas, e is testada com 12 cenários unitários (happy path, falhas, compensação, timeout, idempotência, concorrência).

---

## Como Executar o Projeto

### Pré-requisitos
- **Docker** e **Docker Compose** instalados.
- **.NET SDK 9.0** (opcional, caso queira rodar fora do Docker).

### Usando Docker (Recomendado)
A maneira mais simples de rodar tudo (API + Banco + Monitoramento) é via Docker Compose.

```bash
docker compose --profile default up --build
```

1. Aplique as migrações (se necessário):
   ```bash
   dotnet ef database update --project src/PagueVeloz.Infrastructure --startup-project src/PagueVeloz.API --context ApplicationDbContext  
   ```

A API estará disponível em: `http://localhost:5001`
Documentação Swagger: `http://localhost:5001/swagger`


## Testes

### Testes de Unidade e Integração
O projeto inclui testes cobrindo as regras de domínio e fluxos principais.

```bash
dotnet test
```

### Cobertura de Testes

| Camada               | Testes | Descrição                                                |
|----------------------|--------|----------------------------------------------------------|
| Domain (Account)     | 19     | Credit, debit, reserve, capture, credit limits           |
| Domain (Money)       | -      | Aritmética e comparação de valor monetário               |
| Domain (Currency)    | 10     | Validação de moedas (BRL, USD, EUR)                      |
| Domain (Reversal)    | 11     | Todos os cenários de reversão                            |
| Application          | 7+14   | CreateAccountHandler + ProcessTransactionHandler         |
| Application (Saga)   | 12     | TransferStateMachine (happy path, falha, compensação)    |
| Validators           | 7      | ProcessTransactionValidator                              |
| Integration          | 5+     | Endpoints, concorrência, reserve→capture, reversais      |

### Testes de Carga (K6)
Para validar a performance sob estresse, incluímos um ambiente completo de testes de carga com **K6**, **InfluxDB** e **Grafana**.

Para rodar os testes de carga e ver os gráficos em tempo real:

1. Execute o perfil de testes:
   ```bash
   docker compose --profile k6 up --build
   ```
   *Isso subirá a API, Banco, K6, InfluxDB e Grafana.*

2. Acompanhe os resultados no Grafana:
   - Acesse: `http://localhost:3000`
   - Dashboard: **PagueVeloz - k6 Load Test** (já configurado)

O teste executa cenários de Smoke, Ramp Up (aumento de carga) e Spike (pico de acesso), validando criação de contas, transações e concorrência.

---

## API Endpoints

A API segue o padrão REST com JSON `snake_case`. Todos os valores monetários são em **centavos** (ex: R$ 10,00 = `1000`).

> **Correlation ID**: Todas as requests aceitam o header `X-Correlation-Id` para rastreamento. Se não fornecido, um ID é gerado automaticamente e retornado no response.

### Contas

**Criar Conta**
```http
POST /api/accounts
Content-Type: application/json

{
  "client_id": "CLIENT-001",
  "initial_balance": 1000,
  "credit_limit": 500,
  "currency": "BRL"
}
```

**Consultar Saldo**
```http
GET /api/accounts/{accountId}/balance
```

### Transações

Operações suportadas: `credit`, `debit`, `reserve`, `capture`, `reversal`, `transfer`.

**Credit — Adicionar saldo**
```http
POST /api/transactions
Content-Type: application/json

{
  "operation": "credit",
  "account_id": "{accountId}",
  "amount": 10000,
  "currency": "BRL",
  "reference_id": "TXN-CREDIT-001",
  "metadata": { "description": "Depósito inicial" }
}
```

**Debit — Subtrair saldo (respeita credit limit)**
```http
POST /api/transactions

{
  "operation": "debit",
  "account_id": "{accountId}",
  "amount": 500,
  "currency": "BRL",
  "reference_id": "TXN-DEBIT-001"
}
```

**Reserve — Reservar saldo para captura posterior**
```http
POST /api/transactions

{
  "operation": "reserve",
  "account_id": "{accountId}",
  "amount": 2000,
  "currency": "BRL",
  "reference_id": "TXN-RESERVE-001"
}
```

**Capture — Confirmar valor reservado**
```http
POST /api/transactions

{
  "operation": "capture",
  "account_id": "{accountId}",
  "amount": 2000,
  "currency": "BRL",
  "reference_id": "TXN-CAPTURE-001"
}
```

**Reversal — Reverter transação anterior**
```http
POST /api/transactions

{
  "operation": "reversal",
  "account_id": "{accountId}",
  "amount": 500,
  "currency": "BRL",
  "reference_id": "TXN-REVERSAL-001",
  "metadata": { "original_reference_id": "TXN-DEBIT-001" }
}
```

**Transfer — Transferência entre contas (via Saga)**
```http
POST /api/transactions

{
  "operation": "transfer",
  "account_id": "{sourceAccountId}",
  "destination_account_id": "{destinationAccountId}",
  "amount": 3000,
  "currency": "BRL",
  "reference_id": "TXN-TRANSFER-001"
}
```

**Batch — Processar múltiplas transações**
```http
POST /api/transactions/batch
Content-Type: application/json

[
  { "operation": "credit", "account_id": "{id1}", "amount": 1000, "currency": "BRL", "reference_id": "BATCH-001" },
  { "operation": "debit", "account_id": "{id2}", "amount": 500, "currency": "BRL", "reference_id": "BATCH-002" }
]
```

> **Idempotência**: O campo `reference_id` é chave única. Requests repetidas com mesmo `reference_id` retornam o resultado anterior sem reprocessar.

---

## Observabilidade e Monitoramento

### Health Checks
- `GET /health/live` — Liveness probe (sempre 200)
- `GET /health/ready` — Readiness probe (verifica conexão PostgreSQL)

### Métricas (Prometheus/OpenTelemetry)
Endpoint: `GET /metrics`

| Métrica                                      | Tipo       | Labels                    | Descrição                             |
|----------------------------------------------|------------|---------------------------|---------------------------------------|
| `transactions_total`                         | Counter    | `operation`, `status`     | Total de transações processadas       |
| `transaction_duration_milliseconds`          | Histogram  | `operation`               | Latência de processamento             |
| `transactions_errors_total`                  | Counter    | `operation`, `error_type` | Total de erros por tipo               |

### Correlation ID
Todas as requests são rastreadas via `X-Correlation-Id`:
- Se o header é enviado pelo cliente, é preservado.
- Se ausente, um GUID é gerado automaticamente.
- O ID é incluído no response e propagado nos logs via Serilog `LogContext`.

### Logs
Logs estruturados via **Serilog** (console + arquivo). Cada entrada inclui `CorrelationId`, `RequestPath`, `StatusCode`, garantindo rastreabilidade completa. Compatível com Elasticsearch/Seq.

---

## Decisões de Negócio Extras

- **Concorrência**: Implementado `Optimistic Locking` (bloqueio otimista) com versioning no EF Core para garantir que transações concorrentes na mesma conta não gerem inconsistência de saldo.
- **Idempotência**: O campo `reference_id` é chave única de negócio. Transações repetidas com mesmo ID retornam o resultado anterior (cache-first, fallback para banco).
- **Distributed Locking**: Lock por conta (`account:{id}`) via `SemaphoreSlim` in-memory. Garante serialização de operações na mesma conta. Facilmente substituível por Redis/PostgreSQL advisory locks para multi-instância.
