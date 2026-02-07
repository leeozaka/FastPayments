using MassTransit;

namespace PagueVeloz.Application.Sagas.Transfer;

public sealed class TransferSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int Version { get; set; }
    public string CurrentState { get; set; } = null!;

    public string SourceAccountId { get; set; } = null!;
    public string DestinationAccountId { get; set; } = null!;
    public long Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string ReferenceId { get; set; } = null!;
    public Dictionary<string, string>? Metadata { get; set; }

    public string? DebitTransactionId { get; set; }
    public string? CreditTransactionId { get; set; }

    public long SourceBalance { get; set; }
    public long SourceReservedBalance { get; set; }
    public long SourceAvailableBalance { get; set; }

    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
