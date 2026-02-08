using PagueVeloz.Domain.Enums;

namespace PagueVeloz.Domain.Entities;

public sealed class Transaction : Entity
{
    public string AccountId { get; private set; } = null!;
    public TransactionType Type { get; private set; }
    public long Amount { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public string ReferenceId { get; private set; } = null!;
    public TransactionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? RelatedReferenceId { get; private set; }
    public Dictionary<string, string>? Metadata { get; private set; }
    public DateTime Timestamp { get; private set; }

    private Transaction() { }

    internal static Transaction Create(
        string accountId,
        TransactionType type,
        long amount,
        string currency,
        string referenceId,
        TransactionStatus status,
        Dictionary<string, string>? metadata = null,
        string? errorMessage = null,
        string? relatedReferenceId = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Amount = amount,
            CurrencyCode = currency.ToUpperInvariant(),
            ReferenceId = referenceId,
            Status = status,
            ErrorMessage = errorMessage,
            RelatedReferenceId = relatedReferenceId,
            Metadata = metadata,
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsReversed()
    {
        Status = TransactionStatus.Reversed;
        UpdatedAt = DateTime.UtcNow;
    }
}
