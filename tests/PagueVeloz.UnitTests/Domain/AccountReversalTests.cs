using FluentAssertions;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Exceptions;
using Xunit;

namespace PagueVeloz.UnitTests.Domain;

public class AccountReversalTests
{
    private static Account CreateFundedAccount(long initialCredit = 100_000, long creditLimit = 0)
    {
        var account = Account.Create("ACC-REV", "CLI-001", creditLimit, "BRL");
        account.Credit(initialCredit, "BRL", "TXN-INIT");
        return account;
    }

    [Fact]
    public void ReverseCredit_ShouldDecrementBalance()
    {
        var account = CreateFundedAccount();
        var creditTx = account.Credit(50_000, "BRL", "TXN-CREDIT");

        var reversalTx = account.Reverse(creditTx, "TXN-REV-001");

        reversalTx.Type.Should().Be(TransactionType.Reversal);
        reversalTx.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(100_000);
    }

    [Fact]
    public void ReverseDebit_ShouldIncrementBalance()
    {
        var account = CreateFundedAccount();
        var debitTx = account.Debit(30_000, "BRL", "TXN-DEBIT");

        var reversalTx = account.Reverse(debitTx, "TXN-REV-002");

        reversalTx.Type.Should().Be(TransactionType.Reversal);
        reversalTx.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(100_000);
    }

    [Fact]
    public void ReverseReserve_ShouldDecrementReservedBalance()
    {
        var account = CreateFundedAccount();
        var reserveTx = account.Reserve(40_000, "BRL", "TXN-RESERVE");
        account.ReservedBalance.Should().Be(40_000);

        var reversalTx = account.Reverse(reserveTx, "TXN-REV-003");

        reversalTx.Type.Should().Be(TransactionType.Reversal);
        reversalTx.Status.Should().Be(TransactionStatus.Success);
        account.ReservedBalance.Should().Be(0);
        account.AvailableBalance.Should().Be(100_000);
    }

    [Fact]
    public void ReverseCapture_ShouldIncrementBalance()
    {
        var account = CreateFundedAccount();
        account.Reserve(40_000, "BRL", "TXN-RESERVE");
        var captureTx = account.Capture(40_000, "BRL", "TXN-CAPTURE");
        account.Balance.Should().Be(60_000);

        var reversalTx = account.Reverse(captureTx, "TXN-REV-004");

        reversalTx.Type.Should().Be(TransactionType.Reversal);
        reversalTx.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(100_000);
    }

    [Fact]
    public void Reverse_TransactionFromDifferentAccount_ShouldThrow()
    {
        var account = CreateFundedAccount();
        var otherAccount = Account.Create("ACC-OTHER", "CLI-002", 0, "BRL");
        otherAccount.Credit(50_000, "BRL", "TXN-OTHER-INIT");
        var otherTx = otherAccount.Credit(10_000, "BRL", "TXN-OTHER");

        var act = () => account.Reverse(otherTx, "TXN-REV-005");

        act.Should().Throw<DomainException>()
            .WithMessage("*does not belong to this account*");
    }

    [Fact]
    public void Reverse_FailedTransaction_ShouldThrow()
    {
        var account = CreateFundedAccount(initialCredit: 10_000);
        var failedTx = account.Debit(50_000, "BRL", "TXN-FAIL");
        failedTx.Status.Should().Be(TransactionStatus.Failed);

        var act = () => account.Reverse(failedTx, "TXN-REV-006");

        act.Should().Throw<DomainException>()
            .WithMessage("*only reverse successful transactions*");
    }

    [Fact]
    public void ReverseCredit_WithInsufficientFunds_ShouldThrow()
    {
        var account = CreateFundedAccount(initialCredit: 50_000);
        var creditTx = account.Credit(50_000, "BRL", "TXN-CREDIT");
        account.Debit(80_000, "BRL", "TXN-DRAIN");
        account.Balance.Should().Be(20_000);

        var act = () => account.Reverse(creditTx, "TXN-REV-007");

        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void ReverseReserve_WithInsufficientReserved_ShouldThrow()
    {
        var account = CreateFundedAccount();

        var reserveTx = account.Reserve(40_000, "BRL", "TXN-RESERVE");
        account.Capture(40_000, "BRL", "TXN-CAPTURE");
        account.ReservedBalance.Should().Be(0);

        var act = () => account.Reverse(reserveTx, "TXN-REV-008");

        act.Should().Throw<InsufficientReservedBalanceException>();
    }

    [Fact]
    public void Reverse_OnInactiveAccount_ShouldThrow()
    {
        var account = CreateFundedAccount();
        var creditTx = account.Credit(10_000, "BRL", "TXN-CREDIT");
        account.Deactivate();

        var act = () => account.Reverse(creditTx, "TXN-REV-009");

        act.Should().Throw<InactiveAccountException>();
    }

    [Fact]
    public void Reverse_ShouldRaiseDomainEvents()
    {
        var account = CreateFundedAccount();
        var creditTx = account.Credit(10_000, "BRL", "TXN-CREDIT");
        var eventCountBefore = account.DomainEvents.Count;

        account.Reverse(creditTx, "TXN-REV-010");

        account.DomainEvents.Count.Should().BeGreaterThan(eventCountBefore);
    }

    [Fact]
    public void Reverse_ShouldCreateReversalTransactionWithRelatedReference()
    {
        var account = CreateFundedAccount();
        var creditTx = account.Credit(10_000, "BRL", "TXN-CREDIT");

        var reversalTx = account.Reverse(creditTx, "TXN-REV-011");

        reversalTx.Type.Should().Be(TransactionType.Reversal);
        reversalTx.Amount.Should().Be(10_000);
        reversalTx.RelatedReferenceId.Should().Be("TXN-CREDIT");
        reversalTx.ReferenceId.Should().Be("TXN-REV-011");
    }
}
