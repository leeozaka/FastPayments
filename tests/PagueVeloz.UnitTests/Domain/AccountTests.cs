using FluentAssertions;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Exceptions;
using Xunit;

namespace PagueVeloz.UnitTests.Domain;

public class AccountTests
{
    private static Account CreateActiveAccount(long creditLimit = 0)
    {
        return Account.Create("ACC-001", "CLI-001", creditLimit, "BRL");
    }

    [Fact]
    public void Create_ShouldInitializeWithZeroBalances()
    {
        var account = CreateActiveAccount();

        account.Balance.Should().Be(0);
        account.ReservedBalance.Should().Be(0);
        account.AvailableBalance.Should().Be(0);
        account.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public void Credit_ShouldIncreaseBalance()
    {
        var account = CreateActiveAccount();

        var tx = account.Credit(100000, "BRL", "TXN-001");

        account.Balance.Should().Be(100000);
        account.AvailableBalance.Should().Be(100000);
        tx.Status.Should().Be(TransactionStatus.Success);
    }

    [Fact]
    public void Debit_WithSufficientFunds_ShouldDecreaseBalance()
    {
        var account = CreateActiveAccount();
        account.Credit(100000, "BRL", "TXN-001");

        var tx = account.Debit(20000, "BRL", "TXN-002");

        account.Balance.Should().Be(80000);
        tx.Status.Should().Be(TransactionStatus.Success);
    }

    [Fact]
    public void Debit_WithInsufficientFunds_ShouldReturnFailedTransaction()
    {
        var account = CreateActiveAccount();
        account.Credit(100000, "BRL", "TXN-001");

        var tx = account.Debit(90000_0, "BRL", "TXN-002");

        tx.Status.Should().Be(TransactionStatus.Failed);
        account.Balance.Should().Be(100000);
    }

    [Fact]
    public void Debit_WithCreditLimit_ShouldAllowOverdraft()
    {
        var account = CreateActiveAccount(creditLimit: 50000);
        account.Credit(30000, "BRL", "TXN-001");

        var tx = account.Debit(60000, "BRL", "TXN-002");

        tx.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(-30000);
    }

    [Fact]
    public void Debit_ExceedingCreditLimit_ShouldFail()
    {
        var account = CreateActiveAccount(creditLimit: 50000);
        account.Credit(30000, "BRL", "TXN-001");

        var tx = account.Debit(90000, "BRL", "TXN-002");

        tx.Status.Should().Be(TransactionStatus.Failed);
        account.Balance.Should().Be(30000);
    }

    [Fact]
    public void Reserve_ShouldMoveFromAvailableToReserved()
    {
        var account = CreateActiveAccount();
        account.Credit(100000, "BRL", "TXN-001");

        var tx = account.Reserve(30000, "BRL", "TXN-002");

        tx.Status.Should().Be(TransactionStatus.Success);
        account.ReservedBalance.Should().Be(30000);
        account.AvailableBalance.Should().Be(70000);
        account.Balance.Should().Be(100000);
    }

    [Fact]
    public void Reserve_WithInsufficientAvailableBalance_ShouldFail()
    {
        var account = CreateActiveAccount();
        account.Credit(100000, "BRL", "TXN-001");
        account.Reserve(80000, "BRL", "TXN-002");

        var tx = account.Reserve(30000, "BRL", "TXN-003");

        tx.Status.Should().Be(TransactionStatus.Failed);
    }

    [Fact]
    public void Capture_ShouldReduceReservedAndBalance()
    {
        var account = CreateActiveAccount();
        account.Credit(100000, "BRL", "TXN-001");
        account.Reserve(30000, "BRL", "TXN-002");

        var tx = account.Capture(30000, "BRL", "TXN-003");

        tx.Status.Should().Be(TransactionStatus.Success);
        account.ReservedBalance.Should().Be(0);
        account.Balance.Should().Be(70000);
        account.AvailableBalance.Should().Be(70000);
    }

    [Fact]
    public void Capture_ExceedingReservedBalance_ShouldFail()
    {
        var account = CreateActiveAccount();
        account.Credit(100000, "BRL", "TXN-001");
        account.Reserve(30000, "BRL", "TXN-002");

        var tx = account.Capture(50000, "BRL", "TXN-003");

        tx.Status.Should().Be(TransactionStatus.Failed);
    }

    [Fact]
    public void Operations_OnInactiveAccount_ShouldThrow()
    {
        var account = CreateActiveAccount();
        account.Deactivate();

        var act = () => account.Credit(10000, "BRL", "TXN-001");

        act.Should().Throw<InactiveAccountException>();
    }

    [Fact]
    public void Credit_WithCurrencyMismatch_ShouldThrow()
    {
        var account = CreateActiveAccount();

        var act = () => account.Credit(10000, "USD", "TXN-001");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Currency mismatch*");
    }

    [Fact]
    public void Credit_WithZeroAmount_ShouldThrow()
    {
        var account = CreateActiveAccount();

        var act = () => account.Credit(0, "BRL", "TXN-001");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Credit_ShouldRaiseDomainEvents()
    {
        var account = CreateActiveAccount();

        account.Credit(10000, "BRL", "TXN-001");

        account.DomainEvents.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CaseUse1_BasicCreditAndDebit()
    {
        var account = CreateActiveAccount();

        var tx1 = account.Credit(100000, "BRL", "TXN-001");
        tx1.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(100000);

        var tx2 = account.Debit(20000, "BRL", "TXN-002");
        tx2.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(80000);

        var tx3 = account.Debit(90000, "BRL", "TXN-003");
        tx3.Status.Should().Be(TransactionStatus.Failed);
        account.Balance.Should().Be(80000);
    }

    [Fact]
    public void CaseUse2_OperationsWithCreditLimit()
    {
        var account = Account.Create("ACC-002", "CLI-001", 50000, "BRL");

        var tx1 = account.Credit(30000, "BRL", "TXN-004");
        tx1.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(30000);

        var tx2 = account.Debit(60000, "BRL", "TXN-005");
        tx2.Status.Should().Be(TransactionStatus.Success);
        account.Balance.Should().Be(-30000);

        var tx3 = account.Debit(30000, "BRL", "TXN-006");
        tx3.Status.Should().Be(TransactionStatus.Failed);
        account.Balance.Should().Be(-30000);
    }

    [Fact]
    public void CaseUse3_ReserveAndCapture()
    {
        var account = CreateActiveAccount();

        account.Credit(100000, "BRL", "TXN-001");
        account.Balance.Should().Be(100000);

        var reserve = account.Reserve(30000, "BRL", "TXN-002");
        reserve.Status.Should().Be(TransactionStatus.Success);
        account.AvailableBalance.Should().Be(70000);
        account.ReservedBalance.Should().Be(30000);

        var capture = account.Capture(30000, "BRL", "TXN-003");
        capture.Status.Should().Be(TransactionStatus.Success);
        account.AvailableBalance.Should().Be(70000);
        account.ReservedBalance.Should().Be(0);
    }
}
