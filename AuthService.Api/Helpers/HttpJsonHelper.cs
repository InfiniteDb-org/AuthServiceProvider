using System.Text;
using Newtonsoft.Json;

namespace AuthService.Api.Helpers;

public static class HttpJsonHelper
{
    // posts JSON and deserializes response to T
    public static async Task<T?> PostJsonAsync<T>(HttpClient httpClient, string url, object payload, string? key)
    {
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = FunctionKeyHelper.CreateRequestWithKey(HttpMethod.Post, url, content, key);
        var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return default;

        if (string.IsNullOrWhiteSpace(responseBody))
            return default;

        return JsonConvert.DeserializeObject<T>(responseBody);
    }
}
