namespace PagueVeloz.Application.Sagas.Transfer;

public sealed record TransferRequested
{
    public Guid CorrelationId { get; init; }
    public string SourceAccountId { get; init; } = null!;
    public string DestinationAccountId { get; init; } = null!;
    public long Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string ReferenceId { get; init; } = null!;
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed record DebitSourceCompleted
{
    public Guid CorrelationId { get; init; }
    public string TransactionId { get; init; } = null!;
    public long SourceBalance { get; init; }
    public long SourceReservedBalance { get; init; }
    public long SourceAvailableBalance { get; init; }
}

public sealed record DebitSourceFailed
{
    public Guid CorrelationId { get; init; }
    public string Reason { get; init; } = null!;
}

public sealed record CreditDestinationCompleted
{
    public Guid CorrelationId { get; init; }
    public string TransactionId { get; init; } = null!;
}

public sealed record CreditDestinationFailed
{
    public Guid CorrelationId { get; init; }
    public string Reason { get; init; } = null!;
}

public sealed record CompensateDebitCompleted
{
    public Guid CorrelationId { get; init; }
    public long SourceBalance { get; init; }
    public long SourceReservedBalance { get; init; }
    public long SourceAvailableBalance { get; init; }
}

public sealed record TransferCompleted
{
    public Guid CorrelationId { get; init; }
    public string SourceAccountId { get; init; } = null!;
    public string DestinationAccountId { get; init; } = null!;
    public long Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string ReferenceId { get; init; } = null!;
    public string DebitTransactionId { get; init; } = null!;
    public string CreditTransactionId { get; init; } = null!;
    public long SourceBalance { get; init; }
    public long SourceReservedBalance { get; init; }
    public long SourceAvailableBalance { get; init; }
}

public sealed record TransferFailed
{
    public Guid CorrelationId { get; init; }
    public string SourceAccountId { get; init; } = null!;
    public string DestinationAccountId { get; init; } = null!;
    public long Amount { get; init; }
    public string ReferenceId { get; init; } = null!;
    public string Reason { get; init; } = null!;
}
