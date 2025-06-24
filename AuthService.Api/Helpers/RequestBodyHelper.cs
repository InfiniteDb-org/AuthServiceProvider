using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AuthService.Api.Helpers;

public static class RequestBodyHelper
{
    public static async Task<(bool Succeeded, T? Data, string? Message)> ReadAndValidateRequestBody<T>(HttpRequest req, ILogger logger)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrEmpty(body))
        {
            logger.LogWarning("Request body is empty.");
            return (false, default, "Request body is empty.");
        }
        try
        {
            var request = JsonConvert.DeserializeObject<T>(body);
            if (request == null)
                return (false, default, "Invalid request format.");
            return (true, request, null);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize request body.");
            return (false, default, "Invalid JSON format in request body.");
        }
    }
}
