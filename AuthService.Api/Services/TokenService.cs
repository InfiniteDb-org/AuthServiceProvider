using System.Text;
using AuthService.Api.Models;
using AuthService.Api.Models.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AuthService.Api.Services;

public interface ITokenServiceClient
{
    Task<(bool Succeeded, string? AccessToken)> RequestTokenAsync(string? userId, string email, string role = "User");
}

public class TokenServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<TokenServiceClient> logger) : ITokenServiceClient
{
    public async Task<(bool Succeeded, string? AccessToken)> RequestTokenAsync(string? userId, string email, string role = "User")
    {
        try
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(new { userId, email, role }),
                Encoding.UTF8,
                "application/json");

            // --- FIX: Add x-functions-key header if present ---
            var request = new HttpRequestMessage(HttpMethod.Post, $"{configuration["Providers:TokenServiceProvider"]}/api/GenerateToken")
            {
                Content = content
            };
            var tokenKey = configuration["Providers:TokenServiceProviderKey"];
            if (!string.IsNullOrEmpty(tokenKey))
            {
                request.Headers.Add("x-functions-key", tokenKey);
            }
            var response = await httpClient.SendAsync(request);
                
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Token generation failed: {ErrorContent}", await response.Content.ReadAsStringAsync());
                return (false, null);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResult>(responseJson);
            return (tokenResponse?.Succeeded ?? false, tokenResponse?.AccessToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error requesting token: {Message}", ex.Message);
            return (false, null);
        }
    }
}
