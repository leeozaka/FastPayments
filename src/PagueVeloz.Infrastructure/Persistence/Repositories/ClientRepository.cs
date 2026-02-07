using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Infrastructure.Persistence.Context;

namespace PagueVeloz.Infrastructure.Persistence.Repositories;

public sealed class ClientRepository(IReadDbContext readContext, ApplicationDbContext writeContext) : IClientRepository
{
    public async Task<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await readContext.Clients
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        await writeContext.Clients
            .AddAsync(client, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await readContext.Clients
            .AnyAsync(c => c.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);
    }
}
