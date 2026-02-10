using Ardalis.Result;
using MediatR;
using PagueVeloz.Application.DTOs;

namespace PagueVeloz.Application.UseCases.Accounts;

public sealed record CreateAccountCommand(
    string ClientId,
    string? AccountId,
    long InitialBalance,
    long CreditLimit,
    string Currency
) : IRequest<Result<AccountResponse>>;
