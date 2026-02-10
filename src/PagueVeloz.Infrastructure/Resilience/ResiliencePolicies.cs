using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    public static IServiceCollection AddResiliencePolicies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<ResilienceOptions>()
            .Bind(configuration.GetSection(ResilienceOptions.FileSectionName))
            .ValidateDataAnnotations();

        services.AddResiliencePipeline("default", (builder, context) =>
        {
            var settings = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Default.TimeoutSeconds)
            });

            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = settings.Default.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(settings.Default.RetryDelayMs),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = settings.Default.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(settings.Default.CircuitBreakerSamplingDurationSeconds),
                MinimumThroughput = settings.Default.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.Default.CircuitBreakerBreakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });
        });

        services.AddResiliencePipeline("database", (builder, context) =>
        {
            var settings = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(settings.Database.TimeoutSeconds)
            });

            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = settings.Database.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(settings.Database.RetryDelayMs),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = settings.Database.CircuitBreakerFailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(settings.Database.CircuitBreakerSamplingDurationSeconds),
                MinimumThroughput = settings.Database.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.Database.CircuitBreakerBreakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException)
            });
        });

        return services;
    }
}
