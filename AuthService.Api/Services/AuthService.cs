using AuthService.Api.DTOs;
using AuthService.Api.Models.Responses;
using AuthService.Api.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
        try
        {
            var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
            var createAccountRequest = new { formDto.Email };
            var accountResult = await HttpJsonHelper.PostJsonAsync<AccountServiceResult>(
                _httpClient, _configuration, $"{accountServiceUrl}/api/accounts", createAccountRequest);
            var userId = accountResult.Data?.User?.Id?.ToString();
            var user = accountResult.Data?.User;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("userId could not be extracted from AccountServiceProvider response");
            }
            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formDto.Email, user?.Role ?? "User");
            return new SignUpResult
            {
                Succeeded = true, Message = "Account created successfully", User = user, AccessToken = tokenResult.AccessToken, RefreshToken = tokenResult.RefreshToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignUpAsync");
            return new SignUpResult { Succeeded = false, Message = $"Error: {ex.Message}" };
        }
    }

    // Validates credentials, fetches user, requests tokens
    public async Task<SignInResult> SignInAsync(SignInFormDto formDto)
    {
        try
        {
            var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
            var validateRequest = new { formDto.Email, formDto.Password };
            var accountResult = await HttpJsonHelper.PostJsonAsync<dynamic>(
                _httpClient, _configuration, $"{accountServiceUrl}/api/accounts/validate", validateRequest);

            // log answer for debugging
            /*object asObj = accountResult;
            _logger.LogWarning("AccountService validate response: {AccountResult}", JsonConvert.SerializeObject(asObj));*/
            
            var user = accountResult.data?.user != null ? JsonConvert.DeserializeObject<UserAccountDto>(accountResult.data.user.ToString()) : null;
            var userId = user?.Id?.ToString();
            
            if (string.IsNullOrEmpty(userId))
                return new SignInResult {Succeeded = false, Message = "userId could not be extracted", AccessToken = null, RefreshToken = null };
            
            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formDto.Email, user?.Role ?? "User");
            
            if (!tokenResult.Succeeded || string.IsNullOrEmpty(tokenResult.AccessToken))
                return new SignInResult {Succeeded = false, Message = "Failed to sign in", AccessToken = null, RefreshToken = null };
            
            return new SignInResult
            { Succeeded = true, Message = "Login successful", User = user, AccessToken = tokenResult.AccessToken, RefreshToken = tokenResult.RefreshToken };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignInAsync");
            return new SignInResult { Succeeded = false, Message = $"Error: {ex.Message}", AccessToken = null, RefreshToken = null };
        }
    }

    public Task<bool> SignOutAsync(string userId)
    {
        _logger.LogInformation("User {UserId} signed out at {Time}", userId, DateTime.UtcNow);
        return Task.FromResult(true);
    }
}
