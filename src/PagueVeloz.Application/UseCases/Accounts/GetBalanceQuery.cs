using Ardalis.Result;
using MediatR;
using PagueVeloz.Application.DTOs;

namespace PagueVeloz.Application.UseCases.Accounts;

public sealed record GetBalanceQuery(string AccountId) : IRequest<Result<AccountResponse>>;
