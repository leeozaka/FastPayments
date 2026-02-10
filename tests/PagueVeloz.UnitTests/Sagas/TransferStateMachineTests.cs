using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Infrastructure.Resilience;
using PagueVeloz.Infrastructure.Sagas.Consumers;
using Xunit;

namespace PagueVeloz.UnitTests.Sagas;

public sealed class TransferStateMachineTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private IAccountRepository _accountRepository = null!;
    private ITransactionRepository _transactionRepository = null!;
    private IUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _unitOfWork = new PassthroughUnitOfWork();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:Default:TimeoutSeconds"] = "30",
                ["Resilience:Default:MaxRetryAttempts"] = "1",
                ["Resilience:Default:RetryDelayMs"] = "10",
                ["Resilience:Default:CircuitBreakerFailureRatio"] = "0.5",
                ["Resilience:Default:CircuitBreakerSamplingDurationSeconds"] = "30",
                ["Resilience:Default:CircuitBreakerMinimumThroughput"] = "100",
                ["Resilience:Default:CircuitBreakerBreakDurationSeconds"] = "30",
                ["Resilience:Database:TimeoutSeconds"] = "30",
                ["Resilience:Database:MaxRetryAttempts"] = "1",
                ["Resilience:Database:RetryDelayMs"] = "10",
                ["Resilience:Database:CircuitBreakerFailureRatio"] = "0.5",
                ["Resilience:Database:CircuitBreakerSamplingDurationSeconds"] = "30",
                ["Resilience:Database:CircuitBreakerMinimumThroughput"] = "100",
                ["Resilience:Database:CircuitBreakerBreakDurationSeconds"] = "30"
            })
            .Build();

        _provider = new ServiceCollection()
            .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .AddScoped(_ => _accountRepository)
            .AddScoped(_ => _transactionRepository)
            .AddScoped(_ => _unitOfWork)
            .AddResiliencePolicies(configuration)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DebitSourceConsumer>();
                cfg.AddConsumer<CreditDestinationConsumer>();
                cfg.AddConsumer<CompensateDebitConsumer>();

                cfg.AddSagaStateMachine<TransferStateMachine, TransferSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task TransferRequested_ShouldCreateSagaAndPublishDebitCommand()
    {
        var correlationId = Guid.NewGuid();

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-001",
            DestinationAccountId = "DST-001",
            Amount = 5000,
            Currency = "BRL",
            ReferenceId = "REF-001"
        });

        var sagaHarness = _harness.GetSagaStateMachineHarness<TransferStateMachine, TransferSagaState>();

        (await sagaHarness.Consumed.Any<TransferRequested>()).Should().BeTrue();

        (await _harness.Published.Any<DebitSourceCommand>(
            x => x.Context.Message.CorrelationId == correlationId
                 && x.Context.Message.AccountId == "SRC-001"
                 && x.Context.Message.Amount == 5000
                 && x.Context.Message.ReferenceId == "REF-001-DEBIT")).Should().BeTrue();
    }

    [Fact]
    public async Task SuccessfulDebit_ShouldPublishDebitCompletedAndCreditCommand()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-002", 10000);
        var destAccount = CreateTestAccount("DST-002", 0);

        _accountRepository.GetByAccountIdAsync("SRC-002", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);
        _accountRepository.GetByAccountIdAsync("DST-002", Arg.Any<CancellationToken>())
            .Returns(destAccount);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-002",
            DestinationAccountId = "DST-002",
            Amount = 5000,
            Currency = "BRL",
            ReferenceId = "REF-002"
        });

        (await _harness.Published.Any<DebitSourceCompleted>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        var debitCompleted = _harness.Published.Select<DebitSourceCompleted>()
            .First(x => x.Context.Message.CorrelationId == correlationId);

        debitCompleted.Context.Message.TransactionId.Should().NotBeNullOrEmpty();
        debitCompleted.Context.Message.SourceBalance.Should().Be(5000);

        (await _harness.Published.Any<CreditDestinationCommand>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();
    }

    [Fact]
    public async Task FullTransfer_ShouldComplete()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-003", 10000);
        var destAccount = CreateTestAccount("DST-003", 0);

        _accountRepository.GetByAccountIdAsync("SRC-003", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);
        _accountRepository.GetByAccountIdAsync("DST-003", Arg.Any<CancellationToken>())
            .Returns(destAccount);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-003",
            DestinationAccountId = "DST-003",
            Amount = 3000,
            Currency = "BRL",
            ReferenceId = "REF-003"
        });

        (await _harness.Consumed.Any<DebitSourceCommand>()).Should().BeTrue();
        (await _harness.Consumed.Any<CreditDestinationCommand>()).Should().BeTrue();

        (await _harness.Published.Any<TransferCompleted>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        sourceAccount.Balance.Should().Be(7000);
        destAccount.Balance.Should().Be(3000);
    }

    [Fact]
    public async Task TransferCompleted_ShouldCarryCorrectData()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-004", 10000);
        var destAccount = CreateTestAccount("DST-004", 0);

        _accountRepository.GetByAccountIdAsync("SRC-004", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);
        _accountRepository.GetByAccountIdAsync("DST-004", Arg.Any<CancellationToken>())
            .Returns(destAccount);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-004",
            DestinationAccountId = "DST-004",
            Amount = 4000,
            Currency = "BRL",
            ReferenceId = "REF-004"
        });

        (await _harness.Published.Any<TransferCompleted>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        var published = _harness.Published.Select<TransferCompleted>()
            .First(x => x.Context.Message.CorrelationId == correlationId);
        var message = published.Context.Message;

        message.SourceAccountId.Should().Be("SRC-004");
        message.DestinationAccountId.Should().Be("DST-004");
        message.Amount.Should().Be(4000);
        message.Currency.Should().Be("BRL");
        message.ReferenceId.Should().Be("REF-004");
        message.SourceBalance.Should().Be(6000);
    }

    [Fact]
    public async Task InsufficientFunds_ShouldFailWithoutCompensation()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-005", 1000);

        _accountRepository.GetByAccountIdAsync("SRC-005", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-005",
            DestinationAccountId = "DST-005",
            Amount = 5000,
            Currency = "BRL",
            ReferenceId = "REF-005"
        });

        (await _harness.Published.Any<TransferFailed>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        var failed = _harness.Published.Select<TransferFailed>()
            .First(x => x.Context.Message.CorrelationId == correlationId);

        failed.Context.Message.Reason.Should().Contain("Insufficient funds");

        (await _harness.Published.Any<TransferCompleted>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeFalse();
    }

    [Fact]
    public async Task CreditFails_ShouldCompensateAndPublishFailure()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-006", 10000);

        _accountRepository.GetByAccountIdAsync("SRC-006", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);

        _accountRepository.GetByAccountIdAsync("DST-006", Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-006",
            DestinationAccountId = "DST-006",
            Amount = 3000,
            Currency = "BRL",
            ReferenceId = "REF-006"
        });

        (await _harness.Published.Any<TransferFailed>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        var failed = _harness.Published.Select<TransferFailed>()
            .First(x => x.Context.Message.CorrelationId == correlationId);

        failed.Context.Message.Reason.Should().Contain("compensated");

        (await _harness.Consumed.Any<CompensateDebitCommand>()).Should().BeTrue();
    }

    [Fact]
    public async Task CreditFails_CompensationShouldRestoreSourceBalance()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-007", 10000);

        _accountRepository.GetByAccountIdAsync("SRC-007", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);

        _accountRepository.GetByAccountIdAsync("DST-007", Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-007",
            DestinationAccountId = "DST-007",
            Amount = 3000,
            Currency = "BRL",
            ReferenceId = "REF-007"
        });

        (await _harness.Published.Any<TransferFailed>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        sourceAccount.Balance.Should().Be(10000);
    }

    [Fact]
    public async Task SourceAccountNotFound_ShouldFail()
    {
        var correlationId = Guid.NewGuid();

        _accountRepository.GetByAccountIdAsync("SRC-NOT-FOUND", Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-NOT-FOUND",
            DestinationAccountId = "DST-009",
            Amount = 1000,
            Currency = "BRL",
            ReferenceId = "REF-009"
        });

        (await _harness.Published.Any<TransferFailed>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        var failed = _harness.Published.Select<TransferFailed>()
            .First(x => x.Context.Message.CorrelationId == correlationId);

        failed.Context.Message.Reason.Should().Contain("not found");
    }

    [Fact]
    public async Task FullTransfer_ShouldPersistTransactions()
    {
        var correlationId = Guid.NewGuid();
        var sourceAccount = CreateTestAccount("SRC-010", 20000);
        var destAccount = CreateTestAccount("DST-010", 5000);

        _accountRepository.GetByAccountIdAsync("SRC-010", Arg.Any<CancellationToken>())
            .Returns(sourceAccount);
        _accountRepository.GetByAccountIdAsync("DST-010", Arg.Any<CancellationToken>())
            .Returns(destAccount);

        await _harness.Bus.Publish(new TransferRequested
        {
            CorrelationId = correlationId,
            SourceAccountId = "SRC-010",
            DestinationAccountId = "DST-010",
            Amount = 7000,
            Currency = "BRL",
            ReferenceId = "REF-010"
        });

        (await _harness.Published.Any<TransferCompleted>(
            x => x.Context.Message.CorrelationId == correlationId)).Should().BeTrue();

        await _accountRepository.Received(1).UpdateAsync(sourceAccount, Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).UpdateAsync(destAccount, Arg.Any<CancellationToken>());
        await _transactionRepository.Received(2).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    private static Account CreateTestAccount(string accountId, long initialBalance)
    {
        var account = Account.Create(accountId, "CLI-TEST", 0, "BRL");

        if (initialBalance > 0)
            account.Credit(initialBalance, "BRL", $"{accountId}-INIT");

        account.ClearDomainEvents();
        return account;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
            => await operation(cancellationToken);

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => await operation(cancellationToken);

        public void Dispose() { }
    }
}
