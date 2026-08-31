using System.Diagnostics;
using Gym.Application.Abstractions;
using Gym.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;

namespace Gym.Api.Middleware;

/// <summary>Accepts or creates an X-Correlation-Id, adds it to the response and the log context.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
            && incoming.ToString().Length <= 64
                ? incoming.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>
/// Defense-in-depth CSRF check for the cookie BFF: state-changing requests from an
/// authenticated session must either carry the custom X-CSRF header or declare a JSON
/// content type - neither can be produced by a cross-site HTML form, and cross-site
/// fetch with JSON triggers a CORS preflight. Anonymous requests are unaffected.
/// </summary>
public sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsStateChanging(context.Request.Method)
            && context.User.Identity?.IsAuthenticated == true
            && !context.Request.Headers.ContainsKey(HeaderName)
            && !HasJsonContentType(context.Request))
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"Schreibende Anfragen mit Sitzung erfordern den Header '{HeaderName}' oder Content-Type application/json.",
            };
            problem.Extensions["code"] = "auth.csrf";
            problem.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.HeaderName];
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsStateChanging(string method) =>
        method is "POST" or "PUT" or "PATCH" or "DELETE";

    private static bool HasJsonContentType(HttpRequest request) =>
        request.ContentType is { } contentType
        && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
}

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Concurrent inserts racing a unique constraint (e.g. one active review per user
        // and gym) surface as a regular conflict instead of an internal error.
        if (exception is UniqueConstraintViolationException)
        {
            var conflict = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Konflikt",
                Detail = "Die Anfrage kollidiert mit einem bereits vorhandenen Datensatz.",
                Type = "https://httpstatuses.io/409",
            };
            conflict.Extensions["code"] = "conflict.unique";
            conflict.Extensions["correlationId"] = httpContext.Items[CorrelationIdMiddleware.HeaderName];
            httpContext.Response.StatusCode = conflict.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(conflict, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Interner Fehler",
            Detail = "Ein unerwarteter Fehler ist aufgetreten.",
            Type = "https://httpstatuses.io/500",
        };
        problem.Extensions["correlationId"] = httpContext.Items[CorrelationIdMiddleware.HeaderName];
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}

public static class ResultExtensions
{
    public static ActionResult ToActionResult(this Result result, ControllerBase controller, int successStatus = StatusCodes.Status204NoContent)
    {
        if (result.IsSuccess)
        {
            return controller.StatusCode(successStatus);
        }

        return Problem(controller, result.Error);
    }

    public static ActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
        => result.IsSuccess ? controller.Ok(result.Value) : Problem(controller, result.Error);

    public static ActionResult ToCreatedResult<T>(this Result<T> result, ControllerBase controller)
        => result.IsSuccess
            ? controller.StatusCode(StatusCodes.Status201Created, result.Value)
            : Problem(controller, result.Error);

    private static ObjectResult Problem(ControllerBase controller, Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = error.Type.ToString(),
            Detail = error.Message,
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = controller.HttpContext.Items[CorrelationIdMiddleware.HeaderName];

        return new ObjectResult(problem) { StatusCode = status };
    }
}
