using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Application.Interfaces;

public interface IReadDbContext
{
    IQueryable<Account> Accounts { get; }
    IQueryable<Transaction> Transactions { get; }
    IQueryable<Client> Clients { get; }
}
