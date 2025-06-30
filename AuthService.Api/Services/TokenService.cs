using System.Text;
using AuthService.Api.Models;
using AuthService.Api.Models.Responses;
using AuthService.Api.Helpers;
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
            var payload = JsonConvert.SerializeObject(new { userId, email, role });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            
            var request = FunctionKeyHelper.CreateRequestWithKey(
                configuration,
                HttpMethod.Post,
                $"{configuration["Providers:TokenServiceProvider"]}/api/GenerateToken",
                content,
                configuration["Providers:TokenServiceProviderKey"] 
            );

            foreach (var header in request.Headers)
                logger.LogInformation($"TokenServiceClient HEADER: {header.Key} = {string.Join(",", header.Value)}");
            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            logger.LogInformation($"TokenServiceProvider response: {responseBody}");
                
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Token generation failed: {ErrorContent}", responseBody);
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
