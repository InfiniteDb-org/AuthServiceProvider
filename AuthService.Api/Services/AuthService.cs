using System.Text;
using AuthService.Api.DTOs;
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

    // Helper to add x-functions-key if present
    private HttpRequestMessage CreateRequestWithKey(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };
        var accountKey = _configuration["Providers:AccountServiceProviderKey"];
        if (!string.IsNullOrEmpty(accountKey))
        {
            request.Headers.Add("x-functions-key", accountKey);
        }
        return request;
    }

    public async Task<SignUpResult> SignUpAsync(SignUpFormDto formDto)
    {
        try
        {
            var accountServiceUrl = _configuration["Providers:AccountServiceProvider"];
            var createAccountRequest = new { formDto.Email };
            var accountJson = JsonConvert.SerializeObject(createAccountRequest);
            var accountContent = new StringContent(accountJson, Encoding.UTF8, "application/json");
            var request = CreateRequestWithKey(HttpMethod.Post, $"{accountServiceUrl}/api/accounts", accountContent);
            var accountResponse = await _httpClient.SendAsync(request);

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

            var accountResult = JsonConvert.DeserializeObject<AccountServiceResult>(responseContent);
            var userId = accountResult?.Data?.UserId
                         ?? accountResult?.Data?.Id
                         ?? accountResult?.UserId
                         ?? accountResult?.Id;
            var user = accountResult?.Data?.User;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("userId could not be extracted from AccountServiceProvider response");
            }

            var tokenResult = await _tokenServiceClient.RequestTokenAsync( userId, formDto.Email);

            return new SignUpResult
            {
                Succeeded = true,
                Message = "Account created successfully",
                UserId = userId,
                AccessToken = tokenResult.AccessToken,
                User = user
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
            var request = CreateRequestWithKey(HttpMethod.Post, $"{accountServiceUrl}/api/accounts/validate", validateContent);
            var validateResponse = await _httpClient.SendAsync(request);

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

            var validateResult = JsonConvert.DeserializeObject<AccountServiceResult>(responseContent);
            var userId = validateResult?.Data?.UserId
                         ?? validateResult?.Data?.Id
                         ?? validateResult?.UserId
                         ?? validateResult?.Id;
            var user = validateResult?.Data?.User;
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
                AccessToken = tokenResult.AccessToken,
                User = user
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

    public Task<bool> SignOutAsync(string userId)
    {
        try
        {
            _logger.LogInformation("User {UserId} signed out at {Time}", userId, DateTime.UtcNow);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignOutAsync");
            return Task.FromResult(false);
        }
    }
}
