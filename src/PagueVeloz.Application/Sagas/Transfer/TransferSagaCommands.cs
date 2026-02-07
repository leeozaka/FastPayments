namespace PagueVeloz.Application.Sagas.Transfer;

public sealed record DebitSourceCommand
{
    public Guid CorrelationId { get; init; }
    public string AccountId { get; init; } = null!;
    public long Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string ReferenceId { get; init; } = null!;
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed record CreditDestinationCommand
{
    public Guid CorrelationId { get; init; }
    public string AccountId { get; init; } = null!;
    public long Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string ReferenceId { get; init; } = null!;
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed record CompensateDebitCommand
{
    public Guid CorrelationId { get; init; }
    public string AccountId { get; init; } = null!;
    public long Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string ReferenceId { get; init; } = null!;
}
