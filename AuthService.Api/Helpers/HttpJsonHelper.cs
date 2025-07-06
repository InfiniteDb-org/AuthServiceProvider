
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace AuthService.Api.Helpers
{
    public static class HttpJsonHelper
    {
        // DRY helper for POST JSON and deserialization
        public static async Task<T> PostJsonAsync<T>(HttpClient httpClient, IConfiguration configuration, string url, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = FunctionKeyHelper.CreateRequestWithKey(configuration, HttpMethod.Post, url, content);
            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(responseBody);
        }
    }
}
