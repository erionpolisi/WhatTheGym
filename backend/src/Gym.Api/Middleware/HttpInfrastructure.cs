using System.Diagnostics;
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

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
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
