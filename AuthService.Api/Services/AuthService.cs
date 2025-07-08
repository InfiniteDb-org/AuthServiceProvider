using AuthService.Api.DTOs;
using AuthService.Api.Models.Responses;
using AuthService.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthService.Api.Services;

public interface IAuthService
{
    Task<SignUpResult> SignUpAsync(SignUpFormDto formDto);
    Task<SignInResult> SignInAsync(SignInFormDto formDto);
    Task<bool> SignOutAsync(string userId);
}

public class AuthService(HttpClient httpClient, IConfiguration configuration, ILogger<AuthService> logger, ITokenServiceClient tokenServiceClient) : IAuthService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<AuthService> _logger = logger;
    private readonly ITokenServiceClient _tokenServiceClient = tokenServiceClient;

    // registers user, creates account via AccountService, requests tokens
    public async Task<SignUpResult> SignUpAsync(SignUpFormDto formDto)
    {
        if (string.IsNullOrWhiteSpace(formDto.Email))
            throw new ProblemException("EMAIL_REQUIRED", StatusCodes.Status400BadRequest, "Email is required");
        
        var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
        var endpoint = $"{accountServiceUrl}/api/accounts";
        var createAccountRequest = new { formDto.Email };
        var accountResult = await HttpJsonHelper.PostJsonAsync<AccountServiceResult>(
            _httpClient, endpoint, createAccountRequest, _configuration["Providers:AccountServiceProviderKey"]
        );
        var userId = accountResult?.Data?.User?.Id?.ToString();
        var user = accountResult?.Data?.User;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("userId could not be extracted from AccountServiceProvider response");
            throw new ProblemException("USER_ID_MISSING", StatusCodes.Status400BadRequest, "UserId could not be extracted");
        }
        var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formDto.Email, user?.Role ?? "User");
        if (tokenResult == null || !tokenResult.Succeeded || string.IsNullOrEmpty(tokenResult.AccessToken))
            throw new ProblemException("TOKEN_FAILED", StatusCodes.Status500InternalServerError, "Could not generate access token");

        return new SignUpResult
        {
            Succeeded = true,
            Message = "Account created successfully",
            User = user,
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken
        };
    }

    // Validates credentials, fetches user, requests tokens
    public async Task<SignInResult> SignInAsync(SignInFormDto formDto)
    {
        var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
        var endpoint = $"{accountServiceUrl}/api/accounts/validate";
        var validateRequest = new { formDto.Email, formDto.Password };
        var accountServiceResponse = await HttpJsonHelper.PostJsonAsync<AccountServiceResult>(
            _httpClient, endpoint, validateRequest, _configuration["Providers:AccountServiceProviderKey"]);

        var user = accountServiceResponse?.Data?.User;
        var userId = user?.Id?.ToString();

        if (string.IsNullOrEmpty(userId))
            throw new ProblemException("INVALID_CREDENTIALS", StatusCodes.Status401Unauthorized, "Invalid email or password.");

        var token = await _tokenServiceClient.RequestTokenAsync(userId, formDto.Email, user?.Role ?? "User");
        if (!token.Succeeded || string.IsNullOrEmpty(token.AccessToken))
            throw new ProblemException("TOKEN_FAILED", StatusCodes.Status500InternalServerError, "Failed to sign in.");

        return new SignInResult
        {
            Succeeded = true,
            Message = "Login successful",
            User = user,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken
        };
    }

    public Task<bool> SignOutAsync(string userId)
    {
        _logger.LogInformation("User {UserId} signed out at {Time}", userId, DateTime.UtcNow);
        return Task.FromResult(true);
    }
}
