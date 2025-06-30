using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace AuthService.Api.Helpers;

public static class FunctionKeyHelper
{
    // Helper to add x-functions-key if present, now supports explicit key
    public static HttpRequestMessage CreateRequestWithKey(
        IConfiguration configuration,
        HttpMethod method,
        string url,
        HttpContent? content = null,
        string? key = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };
        var functionKey = key ?? configuration["Providers:AccountServiceProviderKey"];
        if (!string.IsNullOrEmpty(functionKey))
            request.Headers.Add("x-functions-key", functionKey);
        return request;
    }
}
