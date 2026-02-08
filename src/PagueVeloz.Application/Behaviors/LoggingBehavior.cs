using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using Serilog.Context;

namespace PagueVeloz.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger) 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        var disposables = new List<IDisposable>();
        if (request is ITraceable traceable)
        {
            disposables.Add(LogContext.PushProperty("ReferenceId", traceable.ReferenceId));
            disposables.Add(LogContext.PushProperty("AccountId", traceable.AccountId));
            disposables.Add(LogContext.PushProperty("Operation", traceable.Operation));
        }

        try
        {
            logger.LogInformation("Handling {RequestName}", requestName);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next().ConfigureAwait(false);
                stopwatch.Stop();
                logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", 
                    requestName, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex, "Error handling {RequestName} after {ElapsedMs}ms", 
                    requestName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
        finally
        {
            foreach (var d in disposables)
                d.Dispose();
        }
    }
}
