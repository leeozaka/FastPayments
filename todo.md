# PagueVeloz Challenge — TODO Tracker

## Legend

- [ ] Not started
- [x] Done
- [~] Partially done

---

## 1. Domain Layer

- [x] Account entity (balance, reserved balance, credit limit, status, version)
- [x] Client entity (multiple accounts per client)
- [x] Transaction entity (with reference_id, metadata, related_reference_id)
- [x] Base Entity class with domain events support
- [x] Credit operation — adds to balance
- [x] Debit operation — subtracts from balance, respects credit limit
- [x] Reserve operation — moves from available to reserved balance
- [x] Capture operation — confirms reserved amount, decrements both reserved and balance
- [x] Reversal operation in Account entity  
- [x] Transfer — via `TransferDomainService` with pessimistic locking
- [x] Money value object (add, subtract, comparison)
- [x] Currency value object (BRL, USD, EUR)
- [x] Domain exceptions (DuplicateTransaction, InactiveAccount, InsufficientFunds, InsufficientReservedBalance, TransactionNotFound)
- [x] Domain events (BalanceUpdatedEvent, TransactionProcessedEvent)
- [x] Optimistic concurrency (Version field on Account)
- [ ] **Account entity doesn't use Money value object internally** — Balance/ReservedBalance/CreditLimit are raw `long` instead of `Money`. Consider refactoring for stronger type safety.

---

## 2. Application Layer

- [x] MediatR with CQRS (commands/queries)
- [x] CreateAccountCommand + Handler
- [x] GetBalanceQuery + Handler
- [x] ProcessTransactionCommand + Handler (all 6 operation types)
- [x] Idempotency via reference_id check before processing
- [x] FluentValidation for CreateAccount and ProcessTransaction
- [x] Validation pipeline behavior (auto-validates before handler runs)
- [x] Logging pipeline behavior (measures execution time)
- [x] Distributed locking in ProcessTransactionHandler
- [x] UnitOfWork transaction management with rollback
- [x] Domain event publishing after transaction processing
- [x] Reversal handler: validate reversal of reserve/capture/transfer
- [x] **Transfer handler returns only debit transaction** — the credit transaction on the destination account is created by TransferDomainService but the response only includes the debit side. Consider returning both or a composite response.
- [x] **No metadata size/content validation** — metadata dictionary has no limits on key count or value lengths.

---

## 3. Infrastructure Layer

### Persistence
- [x] ApplicationDbContext (write) and ReadDbContext (read-only with AsNoTracking)
- [x] AccountRepository, TransactionRepository, ClientRepository
- [x] UnitOfWork with transaction support
- [x] Entity configurations with unique indexes (AccountId, ReferenceId, ClientId)
- [x] Concurrency token on Account (Version)
- [x] EF Core migrations (initial)

### Concurrency
- [x] InMemoryDistributedLockService (SemaphoreSlim-based)
- [ ] **Replace with Redis or PostgreSQL advisory locks** — in-memory lock won't work in multi-instance deployments. Not strictly required for the challenge but mentioned as a concern.

### Messaging / Events
- [x] InMemoryEventBus (logs events)
- [x] **MassTransit for Saga orchestration** — Async event processing implemented via MassTransit with in-memory transport for saga pattern
- [~] **Event bus only logs — no actual async event processing** — Domain events (BalanceUpdatedEvent, TransactionProcessedEvent) still only log. However, MassTransit now handles async saga events (TransferRequested, DebitSourceCompleted, etc.). Consider migrating domain events to MassTransit or adding background processing.

### Resilience
- [x] ResiliencePolicies.cs exists with retry + exponential backoff config
- [x] Resilience policies are registered but NEVER APPLIED
- [x] Circuit breaker pattern not implemented

---

## 4. API Layer

- [x] POST /api/accounts — create account
- [x] GET /api/accounts/{accountId}/balance — get balance
- [x] POST /api/transactions — process single transaction
- [x] POST /api/transactions/batch — process batch (sequential)
- [x] ExceptionHandlingMiddleware (ValidationException, DomainException, Timeout, generic)
- [x] Serilog structured logging
- [x] Swagger/OpenAPI in development
- [x] Health check endpoint at /health
- [x] Health check has no actual probes
- [ ] **Batch processing is sequential** — consider parallelizing independent transactions or at least noting the design decision.
- [ ] **No API versioning** — not required but good practice.

---

## 5. Observability

- [x] Serilog structured logging
- [x] Logging pipeline behavior (timing)
- [x] **No performance metrics** — Consider adding Prometheus/OpenTelemetry metrics (transaction count, latency histograms, error rates).
- [x] **No transaction tracing/correlation** — Add correlation IDs or OpenTelemetry tracing.
- [x] Health checks incomplete

---

## 6. Testing

### Unit Tests
- [x] AccountTests (19 tests — credit, debit, reserve, capture, credit limits)
- [x] ProcessTransactionValidatorTests (7 tests)
- [x] MoneyTests
- [x] **TransferStateMachineTests (12 comprehensive saga tests)** — Covers happy path, failures, compensation, timeouts, idempotency, concurrent transfers
- [x] **Reversal operation tests** — AccountReversalTests.cs with 11 tests covering all reversal scenarios
- [x] **TransferDomainService** — Saga-based transfer fully tested (unit + integration)
- [x] **Currency value object tests** — CurrencyTests.cs with 10 tests
- [x] **CreateAccountHandler tests** — CreateAccountHandlerTests.cs with 7 tests
- [x] **ProcessTransactionHandler tests** — ProcessTransactionHandlerTests.cs with 14 tests

### Integration Tests
- [x] TransactionEndpointTests (5 tests — create account, credit/debit, insufficient funds, batch, idempotency)
- [x] **Transfer operation testing** — End-to-end API integration test + saga unit tests
- [x] **Concurrent operations test** — TransferStateMachineTests includes concurrent transfer scenarios
- [x] **Reserve + capture flow integration test** — Full reserve → capture API flow verified
- [x] **Reversal flow integration test** — Credit and debit reversal API flows verified
- [x] **Credit limit scenarios integration test** — Overdraft allowed + exceeded scenarios
- [ ] **Integration tests use in-memory DB** — consider adding tests with real PostgreSQL via Testcontainers -- TODO: to study

---

## 7. Docker & Deployment

- [x] Dockerfile (multi-stage build, .NET 9)
- [x] docker-compose.yml (PostgreSQL + API + k6 load testing setup)
- [x] k6 load test script
- [x] Grafana dashboard for k6 results
- [ ] **No deployment scripts** — mentioned as optional deliverable
- [ ] **No environment-specific configurations** — consider adding staging/production appsettings

---

## 8. Documentation

- [x] README.md exists
- [ ] **Review README completeness** — technical decisions, framework justifications, build/run instructions, test instructions, API usage examples
- [ ] **Swagger/OpenAPI documentation** — verify all endpoints are properly documented with examples and response schemas

---

## Priority Order (Next Tasks)

### P1 — High (evaluated aspects)
6. Add performance metrics (transaction count, latency, error rate)
7. Add correlation ID / transaction tracing

### P2 — Medium (polish & diferencial)
8. Replace in-memory distributed lock with PostgreSQL advisory locks
9. Add concurrent operations integration test
10. Improve batch processing (parallel where possible)
11. Review and complete README documentation
12. Ensure Swagger docs have complete request/response examples

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
