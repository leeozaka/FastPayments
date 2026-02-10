using PagueVeloz.Application.Sagas.Transfer;
using Riok.Mapperly.Abstractions;

namespace PagueVeloz.Infrastructure.Mappers;

/// <summary>
/// Source-generated mapper for Saga result mappings.
/// </summary>
[Mapper]
public static partial class SagaResultMapper
{
    /// <summary>
    /// Maps a TransferCompleted saga event to a successful TransferSagaResult.
    /// </summary>
    [MapperIgnoreSource(nameof(TransferCompleted.SourceAccountId))]
    [MapperIgnoreSource(nameof(TransferCompleted.DestinationAccountId))]
    [MapperIgnoreSource(nameof(TransferCompleted.Amount))]
    [MapperIgnoreSource(nameof(TransferCompleted.Currency))]
    [MapperIgnoreSource(nameof(TransferCompleted.ReferenceId))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.Success))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.FailureReason))]
    [MapProperty(nameof(TransferCompleted.CorrelationId), nameof(TransferSagaResult.SagaId))]
    private static partial TransferSagaResult MapCompleted(TransferCompleted completed);

    /// <summary>
    /// Maps a TransferCompleted to a TransferSagaResult, setting Success = true.
    /// </summary>
    public static TransferSagaResult ToResult(this TransferCompleted completed)
    {
        var result = MapCompleted(completed);
        return result with { Success = true };
    }

    /// <summary>
    /// Maps a TransferFailed saga event to a failed TransferSagaResult.
    /// </summary>
    [MapperIgnoreSource(nameof(TransferFailed.SourceAccountId))]
    [MapperIgnoreSource(nameof(TransferFailed.DestinationAccountId))]
    [MapperIgnoreSource(nameof(TransferFailed.Amount))]
    [MapperIgnoreSource(nameof(TransferFailed.ReferenceId))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.Success))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.DebitTransactionId))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.CreditTransactionId))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.SourceBalance))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.SourceReservedBalance))]
    [MapperIgnoreTarget(nameof(TransferSagaResult.SourceAvailableBalance))]
    [MapProperty(nameof(TransferFailed.CorrelationId), nameof(TransferSagaResult.SagaId))]
    [MapProperty(nameof(TransferFailed.Reason), nameof(TransferSagaResult.FailureReason))]
    private static partial TransferSagaResult MapFailed(TransferFailed failed);

    /// <summary>
    /// Maps a TransferFailed to a TransferSagaResult, setting Success = false.
    /// </summary>
    public static TransferSagaResult ToResult(this TransferFailed failed)
    {
        var result = MapFailed(failed);
        return result with { Success = false };
    }
}
