using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.UseCases.Accounts;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;
using Riok.Mapperly.Abstractions;

namespace PagueVeloz.Application.Mappers;

/// <summary>
/// Source-generated mapper for Account-related mappings.
/// </summary>
[Mapper]
public static partial class AccountMapper
{
    /// <summary>
    /// Maps an Account entity to an AccountResponse DTO.
    /// </summary>
    [MapProperty(nameof(Account.CurrencyCode), nameof(AccountResponse.Currency))]
    [MapperIgnoreSource(nameof(Account.Version))]
    [MapperIgnoreSource(nameof(Account.Transactions))]
    [MapperIgnoreSource(nameof(Account.TotalAvailableWithCredit))]
    [MapperIgnoreSource(nameof(Account.Id))]
    [MapperIgnoreSource(nameof(Account.CreatedAt))]
    [MapperIgnoreSource(nameof(Account.UpdatedAt))]
    [MapperIgnoreSource(nameof(Account.DomainEvents))]
    public static partial AccountResponse ToResponse(this Account account);

    /// <summary>
    /// Maps a CreateAccountRequest DTO to a CreateAccountCommand.
    /// </summary>
    public static partial CreateAccountCommand ToCommand(this CreateAccountRequest request);

    /// <summary>
    /// Maps AccountStatus enum to lowercase string to match API contract.
    /// </summary>
    private static string MapAccountStatus(AccountStatus status) =>
        status.ToString().ToLowerInvariant();
}
