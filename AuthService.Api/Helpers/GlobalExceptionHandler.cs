using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthService.Api.Helpers;

// Handles global exception-to-ProblemDetails mapping for Azure Functions
public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    // Handles exceptions globally and returns ProblemDetails
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred. {Message}", exception.Message);
        
        // Only handle ProblemException instances
        if (exception is not ProblemException problemException)
            return false;

        // Create ProblemDetails instance with relevant information
        var problemDetails = new ProblemDetails
        {
            Status = problemException.Status,
            Title = problemException.Error,
            Detail = problemException.Message,
            Type = $"Status:{problemException.Status}:{GetStatusName(problemException.Status)}:{problemException.Error.ToLower()}",
            Extensions =
            { 
                ["code"] = problemException.Error
            }
        };
        if (problemException.Errors is not null)
            problemDetails.Extensions["errors"] = problemException.Errors;

        // Set HTTP response status code and content type
        httpContext.Response.StatusCode = problemException.Status;
        httpContext.Response.ContentType = "application/problem+json";

        // Write ProblemDetails to response
        await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });

        return true;
    }
    
    // Fallback for unexpected errors, returns generic ProblemDetails with trace info
    public static IActionResult CreateProblemResult(Exception exception, HttpContext? httpContext)
    {
        var problem = new ProblemDetails
        {
            Type = exception.GetType().Name,
            Title = "An error occurred",
            Detail = exception.Message,
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext?.Request != null ? $"{httpContext.Request.Method} {httpContext.Request.Path}" : null,
            Extensions = new Dictionary<string, object?>
            {
                { "requestId", httpContext?.TraceIdentifier }
            }
        };
        // Add trace information if available
        var activity = httpContext?.Features?.Get<IHttpActivityFeature>()?.Activity;
        if (activity != null)
            problem.Extensions["traceId"] = activity.Id;

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status500InternalServerError };
    }

    // Returns enum name for status code
    private static string GetStatusName(int status)
    {
        return status switch
        {
            StatusCodes.Status400BadRequest => "BadRequest",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            StatusCodes.Status500InternalServerError => "InternalServerError",
            _ => status.ToString()
        };
    }
}