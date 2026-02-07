namespace PagueVeloz.Application.Interfaces;

public interface IWriteDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
