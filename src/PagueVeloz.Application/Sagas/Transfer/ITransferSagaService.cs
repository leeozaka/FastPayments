using PagueVeloz.Application.DTOs;

namespace PagueVeloz.Application.Sagas.Transfer;

public interface ITransferSagaService
{
    Task<TransferSagaResult> ExecuteTransferAsync(
        string sourceAccountId,
        string destinationAccountId,
        long amount,
        string currency,
        string referenceId,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);
}

public sealed record TransferSagaResult
{
    public bool Success { get; init; }
    public Guid SagaId { get; init; }
    public string? DebitTransactionId { get; init; }
    public string? CreditTransactionId { get; init; }
    public long SourceBalance { get; init; }
    public long SourceReservedBalance { get; init; }
    public long SourceAvailableBalance { get; init; }
    public string? FailureReason { get; init; }
}
