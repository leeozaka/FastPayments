# PagueVeloz Challenge — TODO Tracker

## Legend

- [ ] Not started
- [X] Done

- [~] Partially done

---

## 1. Domain Layer

- [X] Account entity (balance, reserved balance, credit limit, status, version)
- [X] Client entity (multiple accounts per client)
- [X] Transaction entity (with reference_id, metadata, related_reference_id)
- [X] Base Entity class with domain events support
- [X] Credit operation — adds to balance
- [X] Debit operation — subtracts from balance, respects credit limit
- [X] Reserve operation — moves from available to reserved balance
- [X] Capture operation — confirms reserved amount, decrements both reserved and balance
- [X] Reversal operation in Account entity
- [X] Transfer — via `TransferDomainService` with pessimistic locking
- [X] Money value object (add, subtract, comparison)
- [X] Currency value object (BRL, USD, EUR)
- [X] Domain exceptions (DuplicateTransaction, InactiveAccount, InsufficientFunds, InsufficientReservedBalance, TransactionNotFound)
- [X] Domain events (BalanceUpdatedEvent, TransactionProcessedEvent)
- [X] Optimistic concurrency (Version field on Account)
- [ ] **Account entity doesn't use Money value object internally** — Balance/ReservedBalance/CreditLimit are raw `long` instead of `Money`. Consider refactoring for stronger type safety.

---

## 2. Application Layer

- [X] MediatR with CQRS (commands/queries)
- [X] CreateAccountCommand + Handler
- [X] GetBalanceQuery + Handler
- [X] ProcessTransactionCommand + Handler (all 6 operation types)
- [X] Idempotency via reference_id check before processing
- [X] FluentValidation for CreateAccount and ProcessTransaction
- [X] Validation pipeline behavior (auto-validates before handler runs)
- [X] Logging pipeline behavior (measures execution time)
- [X] Distributed locking in ProcessTransactionHandler
- [X] UnitOfWork transaction management with rollback
- [X] Domain event publishing after transaction processing
- [X] Reversal handler: validate reversal of reserve/capture/transfer
- [X] **Transfer handler returns only debit transaction** — the credit transaction on the destination account is created by TransferDomainService but the response only includes the debit side. Consider returning both or a composite response.
- [X] **No metadata size/content validation** — metadata dictionary has no limits on key count or value lengths.

---

## 3. Infrastructure Layer

### Persistence

- [X] ApplicationDbContext (write) and ReadDbContext (read-only with AsNoTracking)
- [X] AccountRepository, TransactionRepository, ClientRepository
- [X] UnitOfWork with transaction support
- [X] Entity configurations with unique indexes (AccountId, ReferenceId, ClientId)
- [X] Concurrency token on Account (Version)
- [X] EF Core migrations (initial)

### Concurrency

- [X] InMemoryDistributedLockService (SemaphoreSlim-based)
- [ ] **Replace with Redis or PostgreSQL advisory locks** — in-memory lock won't work in multi-instance deployments. Not strictly required for the challenge but mentioned as a concern.

### Messaging / Events

- [X] InMemoryEventBus (logs events)
- [X] **MassTransit for Saga orchestration** — Async event processing implemented via MassTransit with in-memory transport for saga pattern

- [~] **Event bus only logs — no actual async event processing** — Domain events (BalanceUpdatedEvent, TransactionProcessedEvent) still only log. However, MassTransit now handles async saga events (TransferRequested, DebitSourceCompleted, etc.). Consider migrating domain events to MassTransit or adding background processing.

### Resilience

- [X] ResiliencePolicies.cs exists with retry + exponential backoff config
- [X] Resilience policies are registered but NEVER APPLIED
- [X] Circuit breaker pattern not implemented

---

## 4. API Layer

- [X] POST /api/accounts — create account
- [X] GET /api/accounts/{accountId}/balance — get balance
- [X] POST /api/transactions — process single transaction
- [X] POST /api/transactions/batch — process batch (sequential)
- [X] ExceptionHandlingMiddleware (ValidationException, DomainException, Timeout, generic)
- [X] Serilog structured logging
- [X] Swagger/OpenAPI in development
- [X] Health check endpoint at /health
- [X] Health check has no actual probes
- [X] **Batch processing is sequential** — consider parallelizing independent transactions or at least noting the design decision.
- [ ] **No API versioning** — not required but good practice.

---

## 5. Observability

- [X] Serilog structured logging
- [X] Logging pipeline behavior (timing)
- [X] **No performance metrics** — Prometheus/OpenTelemetry metrics implemented (transaction count, latency, error rate).
- [X] **No transaction tracing/correlation** — Correlation ID middleware with X-Correlation-Id header.
- [X] Health checks incomplete

---

## 6. Testing

### Unit Tests

- [X] AccountTests (19 tests — credit, debit, reserve, capture, credit limits)
- [X] ProcessTransactionValidatorTests (7 tests)
- [X] MoneyTests
- [X] **TransferStateMachineTests (12 comprehensive saga tests)** — Covers happy path, failures, compensation, timeouts, idempotency, concurrent transfers
- [X] **Reversal operation tests** — AccountReversalTests.cs with 11 tests covering all reversal scenarios
- [X] **TransferDomainService** — Saga-based transfer fully tested (unit + integration)
- [X] **Currency value object tests** — CurrencyTests.cs with 10 tests
- [X] **CreateAccountHandler tests** — CreateAccountHandlerTests.cs with 7 tests
- [X] **ProcessTransactionHandler tests** — ProcessTransactionHandlerTests.cs with 14 tests

### Integration Tests

- [X] TransactionEndpointTests (5 tests — create account, credit/debit, insufficient funds, batch, idempotency)
- [X] **Transfer operation testing** — End-to-end API integration test + saga unit tests
- [X] **Concurrent operations test** — TransferStateMachineTests includes concurrent transfer scenarios
- [X] **Reserve + capture flow integration test** — Full reserve → capture API flow verified
- [X] **Reversal flow integration test** — Credit and debit reversal API flows verified
- [X] **Credit limit scenarios integration test** — Overdraft allowed + exceeded scenarios
- [ ] **Integration tests use in-memory DB** — consider adding tests with real PostgreSQL via Testcontainers -- TODO: to study

---

## 7. Docker & Deployment

- [X] Dockerfile (multi-stage build, .NET 9)
- [X] docker-compose.yml (PostgreSQL + API + k6 load testing setup)
- [X] k6 load test script
- [X] Grafana dashboard for k6 results
- [ ] **No deployment scripts** — mentioned as optional deliverable
- [ ] **No environment-specific configurations** — consider adding staging/production appsettings

---

## 8. Documentation

- [X] README.md exists
- [X] **Review README completeness** — Saga docs, all 6 operations, batch endpoint, correlation ID, metrics table, test coverage summary added.
- [X] **Swagger/OpenAPI documentation** — XML docs, SwaggerOperation annotations, examples on DTOs, enhanced OpenApiInfo.

---

## Priority Order (Next Tasks)

### P1 — High (evaluated aspects)

6. ~~Add performance metrics (transaction count, latency, error rate)~~ ✅
7. ~~Add correlation ID / transaction tracing~~ ✅

### P2 — Medium (polish & diferencial)

8. Replace in-memory distributed lock with PostgreSQL advisory locks
9. Add concurrent operations integration test
10. Improve batch processing (parallel where possible)
11. ~~Review and complete README documentation~~ ✅
12. ~~Ensure Swagger docs have complete request/response examples~~ ✅

### P3 — Low (nice to have)

13. Use Money value object inside Account entity instead of raw longs
14. Add metadata validation (size limits)
15. Add Testcontainers for integration tests with real PostgreSQL
16. Add deployment scripts
17. Add environment-specific appsettings

### Remaining Work

- Migrate domain events (BalanceUpdatedEvent, TransactionProcessedEvent) to MassTransit
- Add end-to-end API integration test for saga-based transfers
- Consider adding saga persistence to PostgreSQL (currently in-memory)
- Document saga pattern choice in README (architectural decision)
