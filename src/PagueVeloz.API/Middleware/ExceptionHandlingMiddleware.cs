using System.Net;
using System.Text.Json;
using FluentValidation;
using PagueVeloz.Domain.Exceptions;

namespace PagueVeloz.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse(
                    "VALIDATION_ERROR",
                    "One or more validation errors occurred.",
                    validationEx.Errors.Select(e => e.ErrorMessage).ToArray())),

            DomainException domainEx => (
                domainEx.Code switch
                {
                    "ACCOUNT_NOT_FOUND" => HttpStatusCode.NotFound,
                    "TRANSACTION_NOT_FOUND" => HttpStatusCode.NotFound,
                    "DUPLICATE_TRANSACTION" => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.UnprocessableEntity
                },
                new ErrorResponse(domainEx.Code, domainEx.Message)),

            TimeoutException => (
                HttpStatusCode.ServiceUnavailable,
                new ErrorResponse("TIMEOUT", "The operation timed out. Please try again.")),

            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse("INTERNAL_ERROR", "An unexpected error occurred."))
        };

        logger.LogError(exception, "Error processing request: {ErrorCode} - {Message}",
            response.Code, response.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public sealed record ErrorResponse(string Code, string Message, string[]? Details = null);
