using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PagueVeloz.IntegrationTests.Fixtures;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "");
        builder.UseEnvironment("Testing");

        builder.UseSetting("Resilience:Default:TimeoutSeconds", "5");
        builder.UseSetting("Resilience:Default:MaxRetryAttempts", "3");
        builder.UseSetting("Resilience:Default:RetryDelayMs", "200");
        builder.UseSetting("Resilience:Default:CircuitBreakerFailureRatio", "0.5");
        builder.UseSetting("Resilience:Default:CircuitBreakerSamplingDurationSeconds", "30");
        builder.UseSetting("Resilience:Default:CircuitBreakerMinimumThroughput", "5");
        builder.UseSetting("Resilience:Default:CircuitBreakerBreakDurationSeconds", "30");

        builder.UseSetting("Resilience:Database:TimeoutSeconds", "10");
        builder.UseSetting("Resilience:Database:MaxRetryAttempts", "2");
        builder.UseSetting("Resilience:Database:RetryDelayMs", "500");
        builder.UseSetting("Resilience:Database:CircuitBreakerFailureRatio", "0.3");
        builder.UseSetting("Resilience:Database:CircuitBreakerSamplingDurationSeconds", "60");
        builder.UseSetting("Resilience:Database:CircuitBreakerMinimumThroughput", "3");
        builder.UseSetting("Resilience:Database:CircuitBreakerBreakDurationSeconds", "60");
    }
}
