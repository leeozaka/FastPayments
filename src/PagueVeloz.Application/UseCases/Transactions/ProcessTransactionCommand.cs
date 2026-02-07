using MediatR;
using PagueVeloz.Application.DTOs;

namespace PagueVeloz.Application.UseCases.Transactions;

public sealed record ProcessTransactionCommand(
    string Operation,
    string AccountId,
    long Amount,
    string Currency,
    string ReferenceId,
    string? DestinationAccountId,
    Dictionary<string, string>? Metadata
) : IRequest<TransactionResponse>;
