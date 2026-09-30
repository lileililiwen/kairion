using Kairion.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Kairion.Api.Errors;

/// <summary>
/// Maps application <see cref="Result{T}"/> values and <see cref="DomainValidationException"/>
/// to ASP.NET Core <see cref="ProblemDetails"/> responses. The MVP returns RFC 7807
/// <c>application/problem+json</c> for every failure so the web app and external clients
/// share a single error contract.
/// </summary>
public static class ProblemDetailsResults
{
    /// <summary>
    /// Builds the appropriate <see cref="IActionResult"/> for a service result. Domain
    /// validation failures are surfaced as 400 with a stable error code; missing entities
    /// are surfaced as 404.
    /// </summary>
    public static IActionResult FromResult<T>(this ControllerBase controller, Result<T> result, string successLocation)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return new ObjectResult(result.Value) { StatusCode = StatusCodes.Status200OK };
        }

        var message = result.Error ?? "Unknown error.";
        if (LooksLikeMissingEntity(message))
        {
            return controller.NotFound(BuildProblem(controller, statusCode: 404, title: "Not found", message, code: "not_found"));
        }
        if (LooksLikeValidation(message))
        {
            return controller.BadRequest(BuildProblem(controller, statusCode: 400, title: "Validation failed", message, code: "validation_failed"));
        }
        if (LooksLikeConfiguration(message))
        {
            return new ObjectResult(BuildProblem(controller, statusCode: 422, title: "Unprocessable entity", message, code: "configuration_invalid"))
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
            };
        }
        if (LooksLikePolicy(message))
        {
            return new ObjectResult(BuildProblem(controller, statusCode: 403, title: "Forbidden", message, code: "policy_denied"))
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
        return new ObjectResult(BuildProblem(controller, statusCode: 422, title: "Operation failed", message, code: "operation_failed"))
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
        };
    }

    /// <summary>
    /// Builds a ProblemDetails response for an uncaught <see cref="DomainValidationException"/>.
    /// </summary>
    public static IActionResult FromDomainException(this ControllerBase controller, DomainValidationException ex)
    {
        return controller.BadRequest(BuildProblem(controller, statusCode: 400, title: "Validation failed", ex.Message, code: "validation_failed"));
    }

    private static bool LooksLikeMissingEntity(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("not found") || lower.Contains("does not exist");
    }

    private static bool LooksLikeValidation(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("required")
            || lower.Contains("invalid")
            || lower.Contains("must ")
            || lower.Contains("cannot be empty")
            || lower.Contains("empty")
            || lower.Contains("whitespace");
    }

    private static bool LooksLikeConfiguration(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("not configured")
            || lower.Contains("provider")
            || lower.Contains("credentials");
    }

    private static bool LooksLikePolicy(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("policy")
            || lower.Contains("forbidden")
            || lower.Contains("denied");
    }

    public static ProblemDetails Build(ControllerBase controller, int statusCode, string title, string message, string code)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = message,
            Type = $"https://kairion.dev/errors/{code}",
            Instance = controller.HttpContext.Request.Path,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;
        return problem;
    }

    private static ProblemDetails BuildProblem(ControllerBase controller, int statusCode, string title, string message, string code)
        => Build(controller, statusCode, title, message, code);
}
