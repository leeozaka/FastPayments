using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Infrastructure.Persistence.Context;

public sealed class ReadDbContext(DbContextOptions<ReadDbContext> options) : DbContext(options), IReadDbContext
{
    public IQueryable<Account> Accounts => Set<Account>().AsNoTracking();
    public IQueryable<Transaction> Transactions => Set<Transaction>().AsNoTracking();
    public IQueryable<Client> Clients => Set<Client>().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
