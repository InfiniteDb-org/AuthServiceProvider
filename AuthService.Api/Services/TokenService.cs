using AuthService.Api.Models.Responses;
using AuthService.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthService.Api.Services;

public interface ITokenServiceClient
{
    Task<TokenResult> RequestTokenAsync(string? userId, string? email, string role = "User");
}

public class TokenServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<TokenServiceClient> logger) : ITokenServiceClient
{
    private readonly ILogger<TokenServiceClient> _logger = logger;

    public async Task<TokenResult> RequestTokenAsync(string? userId, string? email, string role = "User")
    {
        var tokenResult = await HttpJsonHelper.PostJsonAsync<TokenResult>(
            httpClient,
            $"{configuration["Providers:TokenServiceProvider"]}/api/GenerateToken",
            new { userId, email, role },
            configuration["Providers:TokenServiceProviderKey"]
        );
        if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
        {
            throw new ProblemException("TOKEN_DESERIALIZATION_FAILED", StatusCodes.Status502BadGateway, 
                "Failed to deserialize token response or missing access token.");
        }
        return tokenResult;
    }
}
