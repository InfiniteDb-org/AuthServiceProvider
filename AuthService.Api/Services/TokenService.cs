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
    Task<TokenResult> RequestTokenAsync(string? userId, string email, string role = "User");
}

public class TokenServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<TokenServiceClient> logger) : ITokenServiceClient
{
    public async Task<TokenResult> RequestTokenAsync(string? userId, string email, string role = "User")
    {
        try
        {
            var payload = JsonConvert.SerializeObject(new { userId, email, role });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            
            // Build request with function key for TokenService
            var request = FunctionKeyHelper.CreateRequestWithKey(
                configuration,
                HttpMethod.Post,
                $"{configuration["Providers:TokenServiceProvider"]}/api/GenerateToken",
                content,
                configuration["Providers:TokenServiceProviderKey"] 
            );

            // Log headers for debugging
            /*foreach (var header in request.Headers)
                logger.LogInformation($"TokenServiceClient HEADER: {header.Key} = {string.Join(",", header.Value)}");*/
            

            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            /*logger.LogInformation($"TokenServiceProvider response: {responseBody}");*/
                
            if (!response.IsSuccessStatusCode)
            {
                /*logger.LogError("Token generation failed: {ErrorContent}", responseBody);*/
                return new TokenResult { Succeeded = false, AccessToken = null, RefreshToken = null, Message = responseBody };
            }
            
            var tokenResponse = JsonConvert.DeserializeObject<TokenResult>(responseBody);
            return tokenResponse ?? new TokenResult { Succeeded = false, RefreshToken = null, Message = "Deserialization failed" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error requesting token: {Message}", ex.Message);
            return new TokenResult { Succeeded = false, AccessToken = null, RefreshToken = null, Message = ex.Message };
        }
    }
}
