using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Application.UseCases.Transactions;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;
using Xunit;

namespace PagueVeloz.UnitTests.Application;

public class ProcessTransactionHandlerTests
{
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly ITransferSagaService _transferSagaService = Substitute.For<ITransferSagaService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly IDistributedLockService _lockService = Substitute.For<IDistributedLockService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly IMetricsService _metricsService = Substitute.For<IMetricsService>();
    private readonly ProcessTransactionHandler _handler;

    public ProcessTransactionHandlerTests()
    {
        _lockService.AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAsyncDisposable>());

        _unitOfWork.ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<(Transaction, Account)>>>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var func = callInfo.ArgAt<Func<CancellationToken, Task<(Transaction, Account)>>>(0);
                var ct = callInfo.ArgAt<CancellationToken>(1);
                return func(ct);
            });

        _cacheService.GetAsync<TransactionResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();
        _transactionRepository.GetByReferenceIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _handler = new ProcessTransactionHandler(
            _accountRepository, _transactionRepository, _transferSagaService,
            _unitOfWork, _eventBus, _lockService, _cacheService, _metricsService);
    }

    private Account CreateTestAccount(string accountId = "ACC-001", long balance = 100_000, long creditLimit = 0)
    {
        var account = Account.Create(accountId, "CLI-001", creditLimit, "BRL");
        if (balance > 0)
            account.Credit(balance, "BRL", $"{accountId}-INIT");
        return account;
    }

    private void SetupAccountLookup(Account account)
    {
        _accountRepository.GetByAccountIdAsync(account.AccountId, Arg.Any<CancellationToken>())
            .Returns(account);
        _accountRepository.GetByAccountIdReadOnlyAsync(account.AccountId, Arg.Any<CancellationToken>())
            .Returns(account);
    }

    [Fact]
    public async Task Handle_Credit_ShouldProcessSuccessfully()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var command = new ProcessTransactionCommand("credit", "ACC-001", 50_000, "BRL", "TXN-C01", null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
        result.Balance.Should().Be(150_000);
    }

    [Fact]
    public async Task Handle_Debit_ShouldProcessSuccessfully()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var command = new ProcessTransactionCommand("debit", "ACC-001", 30_000, "BRL", "TXN-D01", null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
        result.Balance.Should().Be(70_000);
    }

    [Fact]
    public async Task Handle_Reserve_ShouldProcessSuccessfully()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var command = new ProcessTransactionCommand("reserve", "ACC-001", 40_000, "BRL", "TXN-R01", null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
        result.ReservedBalance.Should().Be(40_000);
    }

    [Fact]
    public async Task Handle_Capture_ShouldProcessSuccessfully()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        account.Reserve(40_000, "BRL", "TXN-R-INIT");
        var command = new ProcessTransactionCommand("capture", "ACC-001", 40_000, "BRL", "TXN-CAP01", null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
    }

    [Fact]
    public async Task Handle_Idempotency_CachedResponse_ShouldReturnCached()
    {
        var cached = new TransactionResponse
        {
            TransactionId = "TXN-CACHED",
            Status = "success",
            Balance = 99_999
        };
        _cacheService.GetAsync<TransactionResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(cached);

        var command = new ProcessTransactionCommand("credit", "ACC-001", 10_000, "BRL", "TXN-DUP", null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.TransactionId.Should().Be("TXN-CACHED");
        result.Balance.Should().Be(99_999);
        await _accountRepository.DidNotReceive().GetByAccountIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Idempotency_DatabaseHit_ShouldReturnExisting()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);

        var existingTx = account.Credit(10_000, "BRL", "TXN-EXIST");
        _transactionRepository.GetByReferenceIdAsync("TXN-EXIST", Arg.Any<CancellationToken>())
            .Returns(existingTx);

        var command = new ProcessTransactionCommand("credit", "ACC-001", 10_000, "BRL", "TXN-EXIST", null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
        await _lockService.DidNotReceive()
            .AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Reversal_ShouldRequireOriginalReferenceId()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var command = new ProcessTransactionCommand("reversal", "ACC-001", 10_000, "BRL", "TXN-REV", null, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*original_reference_id*");
    }

    [Fact]
    public async Task Handle_Reversal_WithValidOriginal_ShouldReverse()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var originalTx = account.Credit(20_000, "BRL", "TXN-ORIG");
        _transactionRepository.GetByReferenceIdAsync("TXN-ORIG", Arg.Any<CancellationToken>())
            .Returns(originalTx);

        var metadata = new Dictionary<string, string> { { "original_reference_id", "TXN-ORIG" } };
        var command = new ProcessTransactionCommand("reversal", "ACC-001", 20_000, "BRL", "TXN-REV-OK", null, metadata);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
    }

    [Fact]
    public async Task Handle_Transfer_ShouldDelegateToSagaService()
    {
        _transferSagaService.ExecuteTransferAsync(
            "ACC-SRC", "ACC-DST", 50_000, "BRL", "TXN-TRF",
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new TransferSagaResult
            {
                Success = true,
                DebitTransactionId = "TXN-TRF-DEBIT",
                CreditTransactionId = "TXN-TRF-CREDIT",
                SourceBalance = 50_000,
                SourceReservedBalance = 0,
                SourceAvailableBalance = 50_000
            });

        var command = new ProcessTransactionCommand("transfer", "ACC-SRC", 50_000, "BRL", "TXN-TRF", "ACC-DST", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be("success");
        result.CreditTransactionId.Should().Be("TXN-TRF-CREDIT");
        await _transferSagaService.Received(1).ExecuteTransferAsync(
            "ACC-SRC", "ACC-DST", 50_000, "BRL", "TXN-TRF",
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Transfer_WithoutDestination_ShouldThrow()
    {
        var command = new ProcessTransactionCommand("transfer", "ACC-SRC", 50_000, "BRL", "TXN-TRF-ND", null, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*destination*");
    }

    [Fact]
    public async Task Handle_InvalidOperation_ShouldThrow()
    {
        var command = new ProcessTransactionCommand("unknown_op", "ACC-001", 10_000, "BRL", "TXN-INVALID", null, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Unknown operation*");
    }

    [Fact]
    public async Task Handle_AccountNotFound_ShouldThrow()
    {
        _accountRepository.GetByAccountIdAsync("ACC-GHOST", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var command = new ProcessTransactionCommand("credit", "ACC-GHOST", 10_000, "BRL", "TXN-GHOST", null, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_ShouldRecordMetrics()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var command = new ProcessTransactionCommand("credit", "ACC-001", 10_000, "BRL", "TXN-METR", null, null);

        await _handler.Handle(command, CancellationToken.None);

        _metricsService.Received(1).RecordTransactionDuration("credit", Arg.Any<double>());
        _metricsService.Received(1).RecordTransactionProcessed("credit", "success");
    }

    [Fact]
    public async Task Handle_ShouldInvalidateBalanceCache()
    {
        var account = CreateTestAccount();
        SetupAccountLookup(account);
        var command = new ProcessTransactionCommand("credit", "ACC-001", 10_000, "BRL", "TXN-CACHE-INV", null, null);

        await _handler.Handle(command, CancellationToken.None);

        await _cacheService.Received().RemoveAsync(
            Arg.Is<string>(k => k.Contains("ACC-001")), Arg.Any<CancellationToken>());
    }
}
