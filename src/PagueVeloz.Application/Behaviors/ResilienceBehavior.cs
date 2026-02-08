using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace PagueVeloz.Application.Behaviors;

public sealed class ResilienceBehavior<TRequest, TResponse>(
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<ResilienceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var pipeline = pipelineProvider.GetPipeline("default");
        
        return await pipeline.ExecuteAsync(
            async ct => await next().ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }
}
