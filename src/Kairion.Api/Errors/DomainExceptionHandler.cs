using Kairion.Application.UseCases;
using Kairion.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kairion.Api.Errors;

/// <summary>
/// Centralised exception handler that converts uncaught <see cref="DomainValidationException"/>
/// into RFC 7807 ProblemDetails and hides any other exception details from clients. The
/// error responses never include provider secrets, raw AI payloads, or unpermitted source
/// data: every failure detail is one of the classified codes below.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is DomainValidationException validation)
        {
            _logger.LogWarning("Domain validation failed: {Message}", validation.Message);
            await WriteProblemAsync(
                httpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                detail: validation.Message,
                code: "validation_failed",
                cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (exception is OperationCanceledException)
        {
            // Client disconnects do not need a body. Rethrow-free path keeps the
            // connection close idiomatic.
            return false;
        }

        _logger.LogError(exception, "Unhandled exception ({ExceptionType}) while processing {Method} {Path}",
            exception.GetType().Name,
            httpContext.Request.Method,
            httpContext.Request.Path);

        var detail = _environment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred. Refer to the trace id when reporting this issue.";

        await WriteProblemAsync(
            httpContext,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal server error",
            detail: detail,
            code: "internal_error",
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string code,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://kairion.dev/errors/{code}",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        // Use the shared application JSON options so the response body matches
        // the rest of the API (camelCase, no null skipping, code/traceId
        // extension fields visible).
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            KairionJsonOptions.Web,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
