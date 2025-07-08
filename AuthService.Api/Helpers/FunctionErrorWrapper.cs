using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AuthService.Api.Helpers;

// Handles exception-to-ProblemDetails mapping for Azure Functions
public static class FunctionErrorWrapper
{
    // Runs function logic and always returns standardized ProblemDetails on error.
    public static async Task<IActionResult> Handle(Func<Task<IActionResult>> func, HttpContext? httpContext = null)
    {
        try
        {
            return await func();
        }
        catch (ProblemException ex)
        {
            // Converts domain exception to ProblemDetails for frontend/API clients
            var problem = new ProblemDetails
            {
                Title = ex.Message, 
                Status = ex.Status, 
                Detail = ex.Message,
                Type = GetProblemTypeUri(ex.Status, ex.Error),
                Extensions =
                {
                    ["code"] = ex.Error 
                }
            };
            // Adds request/trace info for debugging and support
            if (httpContext != null)
            {
                problem.Extensions["requestId"] = httpContext.TraceIdentifier;
                var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;
                if (activity != null)
                    problem.Extensions["traceId"] = activity.Id;
            }
            // Includes validation errors if present
            if (ex.Errors != null)
                problem.Extensions["errors"] = ex.Errors;
            // Always set correct status code on ObjectResult
            return new ObjectResult(problem) { StatusCode = ex.Status };
        }
        catch (Exception ex)
        {
            // Fallback: returns generic ProblemDetails for unexpected errors
            return GlobalExceptionHandler.CreateProblemResult(ex, httpContext);
        }
    }
    
    // Returns unique string for problem type
    private static string GetProblemTypeUri(int status, string? errorCode = null)
    {
        // get enum name for status code, otherwise just status code as string
        var statusName = status switch
        {
            StatusCodes.Status400BadRequest => "BadRequest",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            StatusCodes.Status500InternalServerError => "InternalServerError",
            _ => status.ToString()
        };
        
        return errorCode != null
            ? $"Status:{status}:{statusName}:{errorCode.ToLower()}"
            : $"Status:{status}:{statusName}";
    }
}
