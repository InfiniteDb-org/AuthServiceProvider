using System.Text;
using AuthService.Api.DTOs;
using AuthService.Api.Models;
using AuthService.Api.Models.Responses;
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

    public async Task<SignUpResult> SignUpAsync(SignUpFormDto formDto)
    {
        try
        {
            var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
            var createAccountRequest = new { formDto.Email, formDto.Password };
            var accountJson = JsonConvert.SerializeObject(createAccountRequest);
            var accountContent = new StringContent(accountJson, Encoding.UTF8, "application/json");
            var accountResponse = await _httpClient.PostAsync($"{accountServiceUrl}/api/accounts", accountContent);

            var responseContent = await accountResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("AccountServiceProvider response: {AccountContent}", responseContent);

            if (!accountResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Account creation failed: {ErrorContent}", responseContent);
                return new SignUpResult
                {
                    Succeeded = false,
                    Message = $"Account creation failed: {accountResponse.StatusCode}"
                };
            }

            dynamic? accountResult = JsonConvert.DeserializeObject(responseContent);
            // Försök plocka ut userId oavsett property-namn
            string userId = accountResult.data.userId ?? accountResult.data.id ?? accountResult.userId ?? accountResult.id;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("userId could not be extracted from AccountServiceProvider response");
            }

            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formDto.Email);

            return new SignUpResult
            {
                Succeeded = true,
                Message = "Account created successfully",
                UserId = userId,
                AccessToken = tokenResult.AccessToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignUpAsync");
            return new SignUpResult
            {
                Succeeded = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<SignInResult> SignInAsync(SignInFormDto formDto)
    {
        try
        {
            var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
            var validateRequest = new { formDto.Email, formDto.Password };
            var validateJson = JsonConvert.SerializeObject(validateRequest);
            var validateContent = new StringContent(validateJson, Encoding.UTF8, "application/json");
            var validateResponse = await _httpClient.PostAsync($"{accountServiceUrl}/api/accounts/validate", validateContent);

            var responseContent = await validateResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("AccountServiceProvider validate response: {AccountContent}", responseContent);

            if (!validateResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Credential validation failed: {ErrorContent}", responseContent);
                return new SignInResult
                {
                    Succeeded = false,
                    Message = "Invalid credentials"
                };
            }

            dynamic validateResult = JsonConvert.DeserializeObject(responseContent);
            string userId = validateResult.data?.userId ?? validateResult.data?.id ?? validateResult.userId ?? validateResult.id;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("userId could not be extracted from AccountServiceProvider validate response");
            }

            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formDto.Email);

            return new SignInResult
            {
                Succeeded = true,
                Message = "Login successful",
                UserId = userId,
                AccessToken = tokenResult.AccessToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignInAsync");
            return new SignInResult
            {
                Succeeded = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<bool> SignOutAsync(string userId)
    {
        try
        {
            _logger.LogInformation("User {UserId} signed out at {Time}", userId, DateTime.UtcNow);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignOutAsync");
            return false;
        }
    }
}
