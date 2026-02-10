using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Application.UseCases.Transactions;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using Riok.Mapperly.Abstractions;

namespace PagueVeloz.Application.Mappers;

/// <summary>
/// Source-generated mapper for Transaction-related mappings.
/// </summary>
[Mapper]
public static partial class TransactionMapper
{
    /// <summary>
    /// Maps a TransactionRequest DTO to a ProcessTransactionCommand.
    /// </summary>
    public static partial ProcessTransactionCommand ToCommand(this TransactionRequest request);

    /// <summary>
    /// Maps a Transaction entity and Account entity to a TransactionResponse DTO.
    /// User-implemented because it requires two source objects and computed properties.
    /// </summary>
    public static TransactionResponse ToResponse(this Transaction transaction, Account? account)
    {
        return new TransactionResponse
        {
            TransactionId = $"{transaction.ReferenceId}-PROCESSED",
            Status = transaction.Status.ToString().ToLowerInvariant(),
            Balance = account?.Balance ?? 0,
            ReservedBalance = account?.ReservedBalance ?? 0,
            AvailableBalance = account?.AvailableBalance ?? 0,
            Timestamp = transaction.Timestamp,
            ErrorMessage = transaction.ErrorMessage
        };
    }

    /// <summary>
    /// Maps a successful TransferSagaResult to a TransactionResponse DTO.
    /// User-implemented because success/failure paths differ.
    /// </summary>
    public static TransactionResponse ToResponse(this TransferSagaResult result)
    {
        if (!result.Success)
        {
            return new TransactionResponse
            {
                TransactionId = $"{result.DebitTransactionId ?? "unknown"}-PROCESSED",
                Status = "failed",
                Balance = result.SourceBalance,
                ReservedBalance = result.SourceReservedBalance,
                AvailableBalance = result.SourceAvailableBalance,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = result.FailureReason
            };
        }

        return new TransactionResponse
        {
            TransactionId = $"{result.DebitTransactionId}-PROCESSED",
            CreditTransactionId = result.CreditTransactionId,
            Status = "success",
            Balance = result.SourceBalance,
            ReservedBalance = result.SourceReservedBalance,
            AvailableBalance = result.SourceAvailableBalance,
            Timestamp = DateTime.UtcNow
        };
    }
}
