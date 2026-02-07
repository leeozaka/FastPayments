using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Domain.Interfaces.Services;
using PagueVeloz.Domain.Services;
using PagueVeloz.Infrastructure.Concurrency;
using PagueVeloz.Infrastructure.Messaging;
using PagueVeloz.Infrastructure.Persistence;
using PagueVeloz.Infrastructure.Persistence.Context;
using PagueVeloz.Infrastructure.Persistence.Repositories;
using PagueVeloz.Infrastructure.Resilience;
using PagueVeloz.Infrastructure.Sagas;
using PagueVeloz.Infrastructure.Sagas.Consumers;

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
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                });
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

        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumer<DebitSourceConsumer>();
            cfg.AddConsumer<CreditDestinationConsumer>();
            cfg.AddConsumer<CompensateDebitConsumer>();

            cfg.AddSagaStateMachine<TransferStateMachine, TransferSagaState>()
                .InMemoryRepository();

            cfg.UsingInMemory((context, busConfig) =>
            {
                busConfig.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<ITransferSagaService, TransferSagaService>();

        services.AddResiliencePolicies();

        return services;
    }
}
