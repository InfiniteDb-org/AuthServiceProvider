using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Helpers;

public static class ActionResultHelper
{
    public static OkObjectResult Ok(object result) => new(result);
    public static BadRequestObjectResult BadRequest(string? message) => new(new { Succeeded = false, Message = message });
    private static NotFoundObjectResult NotFound(string message) => new(new { Succeeded = false, Message = message });
    private static ConflictObjectResult Conflict(string message) => new(new { Succeeded = false, Message = message });
    private static UnauthorizedObjectResult Unauthorized(string message) => new(new { Succeeded = false, Message = message });

    public static IActionResult CreateResponse(dynamic result)
    {
        if (result.Succeeded == true)
            return Ok(result);

        var message = (result.Message as string)?.ToLowerInvariant();
        if (message?.Contains("not found") == true)
            return NotFound(result.Message);
        if (message?.Contains("invalid credentials") == true)
            return Unauthorized(result.Message);
        if (message?.Contains("already exists") == true)
            return Conflict(result.Message);
        return BadRequest(result.Message);
    }
}
