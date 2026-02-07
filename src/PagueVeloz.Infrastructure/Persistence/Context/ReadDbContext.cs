using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Infrastructure.Persistence.Context;

public sealed class ReadDbContext(ApplicationDbContext context) : IReadDbContext
{
    public IQueryable<Account> Accounts => context.Accounts.AsNoTracking();
    public IQueryable<Transaction> Transactions => context.Transactions.AsNoTracking();
    public IQueryable<Client> Clients => context.Clients.AsNoTracking();
}
