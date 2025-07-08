using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthService.Api.Helpers;

public static class ProxyHelper
{
    public enum ProxyTarget
    {
        AccountService,
        TokenService
    }

    // proxies HTTP request to downstream service, preserves status and body
    public static async Task<IActionResult> Proxy(HttpRequest req, string url, HttpMethod method, ProxyTarget target, IConfiguration config, HttpClient httpClient, ILogger logger)
    {
        string? requestBody = null;
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
        {
            requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            logger.LogWarning("ProxyHelper: Incoming requestBody: {Body}", requestBody);
        }

        HttpContent? content = null;
        if (requestBody != null)
            content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        
        var key = target switch
        {
            ProxyTarget.AccountService => config["Providers:AccountServiceProviderKey"],
            ProxyTarget.TokenService => config["Providers:TokenServiceProviderKey"],
            _ => null
        };
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException($"Function key missing for {target}");

        var httpRequest = FunctionKeyHelper.CreateRequestWithKey(method, url, content, key);

        try
        {
            var response = await httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseBody,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Proxy error to {Url}", url);
            return new BadRequestObjectResult(new { Succeeded = false, Message = "Internal server error" });
        }
    }
}