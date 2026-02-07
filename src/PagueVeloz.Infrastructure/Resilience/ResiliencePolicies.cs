using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace PagueVeloz.Infrastructure.Resilience;

public static class ResiliencePolicies
{
    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        services.AddResiliencePipeline("default", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not InvalidOperationException)
            });
        });

        services.AddResiliencePipeline("database", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder().Handle<TimeoutException>()
            });
        });

        return services;
    }
}
