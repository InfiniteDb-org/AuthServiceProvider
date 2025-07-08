namespace AuthService.Api.Helpers;

public static class FunctionKeyHelper
{
    public static HttpRequestMessage CreateRequestWithKey(
        HttpMethod method, string url, HttpContent? content, string? key)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };
        
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.Add("x-functions-key", key);
        }
        
        return request;
    }
}
