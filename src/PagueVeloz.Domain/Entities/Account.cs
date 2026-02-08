using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Events;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.ValueObjects;

namespace PagueVeloz.Domain.Entities;

public sealed class Account : Entity
{
    public string AccountId { get; private set; } = null!;
    public string ClientId { get; private set; } = null!;
    public long Balance { get; private set; }
    public long ReservedBalance { get; private set; }
    public long CreditLimit { get; private set; }
    public AccountStatus Status { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public int Version { get; private set; }

    private readonly List<Transaction> _transactions = [];
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public long AvailableBalance => Balance - ReservedBalance;
    public long TotalAvailableWithCredit => AvailableBalance + CreditLimit;

    private Account() { }

    public static Account Create(string accountId, string clientId, long creditLimit, string currency)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID is required.", nameof(accountId));

        Currency.Create(currency);

        return new Account
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ClientId = clientId,
            Balance = 0,
            ReservedBalance = 0,
            CreditLimit = creditLimit,
            Status = AccountStatus.Active,
            CurrencyCode = currency.ToUpperInvariant(),
            Version = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public Transaction Credit(long amount, string currency, string referenceId, Dictionary<string, string>? metadata = null)
    {
        EnsureActive();
        ValidateAmount(amount);
        ValidateCurrency(currency);

        var previousBalance = Balance;
        Balance += amount;
        UpdatedAt = DateTime.UtcNow;

        var transaction = Transaction.Create(
            AccountId, TransactionType.Credit, amount, currency,
            referenceId, TransactionStatus.Success, metadata);

        _transactions.Add(transaction);

        RaiseDomainEvent(new TransactionProcessedEvent(
            transaction.Id, AccountId, TransactionType.Credit,
            amount, currency, TransactionStatus.Success,
            AvailableBalance, ReservedBalance));

        RaiseDomainEvent(new BalanceUpdatedEvent(
            AccountId, previousBalance, Balance, ReservedBalance, AvailableBalance));

        return transaction;
    }

    public Transaction Debit(long amount, string currency, string referenceId, Dictionary<string, string>? metadata = null)
    {
        EnsureActive();
        ValidateAmount(amount);
        ValidateCurrency(currency);

        if (amount > TotalAvailableWithCredit)
        {
            var failedTx = Transaction.Create(
                AccountId, TransactionType.Debit, amount, currency,
                referenceId, TransactionStatus.Failed, metadata,
                "Insufficient funds (including credit limit)");

            _transactions.Add(failedTx);

            RaiseDomainEvent(new TransactionProcessedEvent(
                failedTx.Id, AccountId, TransactionType.Debit,
                amount, currency, TransactionStatus.Failed,
                AvailableBalance, ReservedBalance));

            return failedTx;
        }

        var previousBalance = Balance;
        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;

        var transaction = Transaction.Create(
            AccountId, TransactionType.Debit, amount, currency,
            referenceId, TransactionStatus.Success, metadata);

        _transactions.Add(transaction);

        RaiseDomainEvent(new TransactionProcessedEvent(
            transaction.Id, AccountId, TransactionType.Debit,
            amount, currency, TransactionStatus.Success,
            AvailableBalance, ReservedBalance));

        RaiseDomainEvent(new BalanceUpdatedEvent(
            AccountId, previousBalance, Balance, ReservedBalance, AvailableBalance));

        return transaction;
    }

    public Transaction Reserve(long amount, string currency, string referenceId, Dictionary<string, string>? metadata = null)
    {
        EnsureActive();
        ValidateAmount(amount);
        ValidateCurrency(currency);

        if (amount > AvailableBalance)
        {
            var failedTx = Transaction.Create(
                AccountId, TransactionType.Reserve, amount, currency,
                referenceId, TransactionStatus.Failed, metadata,
                "Insufficient available balance for reservation");

            _transactions.Add(failedTx);

            RaiseDomainEvent(new TransactionProcessedEvent(
                failedTx.Id, AccountId, TransactionType.Reserve,
                amount, currency, TransactionStatus.Failed,
                AvailableBalance, ReservedBalance));

            return failedTx;
        }

        ReservedBalance += amount;
        UpdatedAt = DateTime.UtcNow;

        var transaction = Transaction.Create(
            AccountId, TransactionType.Reserve, amount, currency,
            referenceId, TransactionStatus.Success, metadata);

        _transactions.Add(transaction);

        RaiseDomainEvent(new TransactionProcessedEvent(
            transaction.Id, AccountId, TransactionType.Reserve,
            amount, currency, TransactionStatus.Success,
            AvailableBalance, ReservedBalance));

        return transaction;
    }

    public Transaction Capture(long amount, string currency, string referenceId, Dictionary<string, string>? metadata = null)
    {
        EnsureActive();
        ValidateAmount(amount);
        ValidateCurrency(currency);

        if (amount > ReservedBalance)
        {
            var failedTx = Transaction.Create(
                AccountId, TransactionType.Capture, amount, currency,
                referenceId, TransactionStatus.Failed, metadata,
                "Insufficient reserved balance for capture");

            _transactions.Add(failedTx);

            RaiseDomainEvent(new TransactionProcessedEvent(
                failedTx.Id, AccountId, TransactionType.Capture,
                amount, currency, TransactionStatus.Failed,
                AvailableBalance, ReservedBalance));

            return failedTx;
        }

        var previousBalance = Balance;
        ReservedBalance -= amount;
        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;

        var transaction = Transaction.Create(
            AccountId, TransactionType.Capture, amount, currency,
            referenceId, TransactionStatus.Success, metadata);

        _transactions.Add(transaction);

        RaiseDomainEvent(new TransactionProcessedEvent(
            transaction.Id, AccountId, TransactionType.Capture,
            amount, currency, TransactionStatus.Success,
            AvailableBalance, ReservedBalance));

        RaiseDomainEvent(new BalanceUpdatedEvent(
            AccountId, previousBalance, Balance, ReservedBalance, AvailableBalance));

        return transaction;
    }

    public Transaction Reverse(Transaction originalTransaction, string referenceId, Dictionary<string, string>? metadata = null)
    {
        EnsureActive();
        ValidateCurrency(originalTransaction.CurrencyCode);

        if (originalTransaction.AccountId != AccountId)
            throw new DomainException("INVALID_REVERSAL", "Transaction does not belong to this account.");

        if (originalTransaction.Status != TransactionStatus.Success)
            throw new DomainException("INVALID_REVERSAL", "Can only reverse successful transactions.");

        var previousBalance = Balance;
        var previousReservedBalance = ReservedBalance;

        switch (originalTransaction.Type)
        {
            case TransactionType.Credit:
                if (originalTransaction.Amount > TotalAvailableWithCredit)
                    throw new InsufficientFundsException(AccountId, originalTransaction.Amount, TotalAvailableWithCredit);
                Balance -= originalTransaction.Amount;
                break;

            case TransactionType.Debit:
                Balance += originalTransaction.Amount;
                break;

            case TransactionType.Reserve:
                if (originalTransaction.Amount > ReservedBalance)
                    throw new InsufficientReservedBalanceException(AccountId, originalTransaction.Amount, ReservedBalance);
                ReservedBalance -= originalTransaction.Amount;
                break;

            case TransactionType.Capture:
                Balance += originalTransaction.Amount;
                break;

            default:
                throw new DomainException("INVALID_REVERSAL",
                    $"Cannot reverse operation of type {originalTransaction.Type}.");
        }

        UpdatedAt = DateTime.UtcNow;

        var transaction = Transaction.Create(
            AccountId, TransactionType.Reversal, originalTransaction.Amount,
            originalTransaction.CurrencyCode, referenceId, TransactionStatus.Success,
            metadata, null, originalTransaction.ReferenceId);

        _transactions.Add(transaction);

        RaiseDomainEvent(new TransactionProcessedEvent(
            transaction.Id, AccountId, TransactionType.Reversal,
            originalTransaction.Amount, originalTransaction.CurrencyCode,
            TransactionStatus.Success, AvailableBalance, ReservedBalance));

        if (previousBalance != Balance)
        {
            RaiseDomainEvent(new BalanceUpdatedEvent(
                AccountId, previousBalance, Balance, ReservedBalance, AvailableBalance));
        }

        return transaction;
    }

    public void Activate()
    {
        Status = AccountStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Block()
    {
        Status = AccountStatus.Blocked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = AccountStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCreditLimit(long newLimit)
    {
        if (newLimit < 0)
            throw new ArgumentException("Credit limit cannot be negative.", nameof(newLimit));

        CreditLimit = newLimit;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureActive()
    {
        if (Status != AccountStatus.Active)
            throw new InactiveAccountException(AccountId);
    }

    private static void ValidateAmount(long amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
    }

    private void ValidateCurrency(string currency)
    {
        if (!string.Equals(CurrencyCode, currency?.ToUpperInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Currency mismatch. Account currency: {CurrencyCode}, Operation currency: {currency}");
    }

    internal void IncrementVersion()
    {
        Version++;
    }
}
