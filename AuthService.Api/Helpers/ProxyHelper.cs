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

    public static async Task<IActionResult> Proxy(HttpRequest req, string url, HttpMethod method, ProxyTarget target, IConfiguration config, HttpClient httpClient, ILogger logger)
    {
        string? requestBody = null;
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
            requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        HttpContent? content = null;
        if (requestBody != null)
            content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var key = target switch
        {
            ProxyTarget.AccountService => config["Providers:AccountServiceProviderKey"],
            ProxyTarget.TokenService => config["Providers:TokenServiceProviderKey"],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };

        var httpRequest = FunctionKeyHelper.CreateRequestWithKey(config, method, url, content, key);

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
            logger.LogError(ex, $"Proxy error to {url}");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }
}