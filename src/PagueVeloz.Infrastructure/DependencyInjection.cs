using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Infrastructure.Caching;
using PagueVeloz.Infrastructure.Concurrency;
using PagueVeloz.Infrastructure.Messaging;
using PagueVeloz.Infrastructure.Metrics;
using PagueVeloz.Infrastructure.Persistence;
using PagueVeloz.Infrastructure.Persistence.Context;
using PagueVeloz.Infrastructure.Persistence.Repositories;
using PagueVeloz.Infrastructure.Resilience;
using PagueVeloz.Infrastructure.Sagas;
using PagueVeloz.Infrastructure.Sagas.Consumers;
using PagueVeloz.Infrastructure.Storage;

namespace PagueVeloz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var writeConnectionString = configuration.GetConnectionString("DefaultConnection");
        var readConnectionString = configuration.GetConnectionString("ReadConnection");

        if (string.IsNullOrEmpty(readConnectionString))
            readConnectionString = writeConnectionString;

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(writeConnectionString))
            {
                options.UseNpgsql(writeConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(30);
                });
            }
            else
            {
                options.UseInMemoryDatabase("PagueVelozDb");
            }
        });

        services.AddDbContext<ReadDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(readConnectionString))
            {
                options.UseNpgsql(readConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(15);
                });
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
            else
            {
                options.UseInMemoryDatabase("PagueVelozDb");
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
        });

        services.AddScoped<IReadDbContext>(sp => sp.GetRequiredService<ReadDbContext>());
        services.AddScoped<IWriteDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<ICacheService, InMemoryCacheService>();
        services.AddSingleton<IKeyValueStore, InMemoryKeyValueStore>();

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

        services.AddSingleton<TransferSagaService>();
        services.AddSingleton<ITransferSagaService>(sp => sp.GetRequiredService<TransferSagaService>());

        services.AddResiliencePolicies(configuration);

        return services;
    }
}
