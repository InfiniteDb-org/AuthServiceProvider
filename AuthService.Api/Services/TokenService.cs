using System.Text;
using AuthService.Api.Models;
using AuthService.Api.Models.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AuthService.Api.Services;

public interface ITokenServiceClient
{
    Task<(bool Succeeded, string? AccessToken)> RequestTokenAsync(string userId, string email, string role = "User");
}

public class TokenServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<TokenServiceClient> logger) : ITokenServiceClient
{
    public async Task<(bool Succeeded, string? AccessToken)> RequestTokenAsync(string userId, string email, string role = "User")
    {
        try
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(new { userId, email, role }),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(
                $"{configuration["Providers:TokenServiceProvider"]}/api/GenerateToken", 
                content);
                
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Token generation failed: {ErrorContent}", await response.Content.ReadAsStringAsync());
                return (false, null);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseJson);
            return (tokenResponse?.Succeeded ?? false, tokenResponse?.AccessToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error requesting token: {Message}", ex.Message);
            return (false, null);
        }
    }
}
