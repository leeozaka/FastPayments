using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Domain.Interfaces.Services;
using PagueVeloz.Domain.Services;
using PagueVeloz.Infrastructure.Concurrency;
using PagueVeloz.Infrastructure.Messaging;
using PagueVeloz.Infrastructure.Persistence;
using PagueVeloz.Infrastructure.Persistence.Context;
using PagueVeloz.Infrastructure.Persistence.Repositories;
using PagueVeloz.Infrastructure.Resilience;

namespace PagueVeloz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("PagueVelozDb");
            }
        });

        services.AddScoped<IReadDbContext, ReadDbContext>();
        services.AddScoped<IWriteDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ITransferService, TransferDomainService>();
        services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        services.AddResiliencePolicies();

        return services;
    }
}
