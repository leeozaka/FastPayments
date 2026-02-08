using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Domain.Exceptions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace PagueVeloz.Infrastructure.Resilience;

public static class ResiliencePolicies
{
    private static bool IsTransientException(Exception ex) =>
        ex is not DomainException
            and not ValidationException
            and not InvalidOperationException
            and not ArgumentException
            and not OperationCanceledException;

    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services)
    {
        services.AddResiliencePipeline("default", builder =>
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(5)
            });

            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });
        });

        services.AddResiliencePipeline("database", builder =>
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            });

            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.3,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });
        });

        return services;
    }
}
