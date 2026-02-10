using System.ComponentModel.DataAnnotations;

namespace PagueVeloz.Infrastructure.Resilience;

public sealed class ResilienceOptions
{
    public const string FileSectionName = "Resilience";

    [Required]
    public ResiliencePipelineSettings Default { get; set; } = new();

    [Required]
    public ResiliencePipelineSettings Database { get; set; } = new();
}

public sealed class ResiliencePipelineSettings
{
    [Range(minimum: 0.1, maximum: double.MaxValue)]
    public double TimeoutSeconds { get; set; }

    [Range(minimum: 0, maximum: int.MaxValue)]
    public int MaxRetryAttempts { get; set; }

    [Range(minimum: 0, maximum: int.MaxValue)]
    public int RetryDelayMs { get; set; }

    [Range(minimum: 0, maximum: 1)]
    public double CircuitBreakerFailureRatio { get; set; }

    [Range(minimum: 0, maximum: int.MaxValue)]
    public int CircuitBreakerSamplingDurationSeconds { get; set; }

    [Range(minimum: 0, maximum: int.MaxValue)]
    public int CircuitBreakerMinimumThroughput { get; set; }

    [Range(minimum: 0, maximum: int.MaxValue)]
    public int CircuitBreakerBreakDurationSeconds { get; set; }
}
