using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using MediatR;

namespace PagueVeloz.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next().ConfigureAwait(false);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false);

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            if (IsResultType(typeof(TResponse)))
            {
                var errors = failures.Select(f => new ValidationError(f.ErrorMessage)).ToList();
                return (TResponse)CreateInvalidResult(typeof(TResponse), errors);
            }

            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }

    private static bool IsResultType(Type type)
    {
        if (type == typeof(Result))
            return true;

        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>);
    }

    private static object CreateInvalidResult(Type resultType, List<ValidationError> errors)
    {
        if (resultType == typeof(Result))
            return Result.Invalid(errors);

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var invalidMethod = typeof(Result<>)
                .MakeGenericType(resultType.GetGenericArguments()[0])
                .GetMethod(nameof(Result<object>.Invalid), [typeof(List<ValidationError>)]);

            return invalidMethod!.Invoke(null, [errors])!;
        }

        throw new InvalidOperationException($"Cannot create invalid result for type {resultType}");
    }
}
