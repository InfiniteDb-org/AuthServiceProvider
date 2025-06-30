using System.Text;
using AuthService.Api.DTOs;
using AuthService.Api.Services;
using AuthService.Api.Helpers;
using AuthService.Api.Models.Requests;
using AuthService.Api.Models.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace AuthService.Api.Functions;

public class AuthFunctions(ILogger<AuthFunctions> logger, IAuthService authService, IConfiguration configuration, HttpClient httpClient, ITokenServiceClient tokenServiceClient)
{
    private readonly ILogger<AuthFunctions> _logger = logger;
    private readonly IAuthService _authService = authService;
    private readonly IConfiguration _configuration = configuration;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenServiceClient _tokenServiceClient = tokenServiceClient;

    
    [Function("SignIn")]
    public async Task<IActionResult> SignIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signin")] HttpRequest req)
    {
        try
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<SignInFormDto>(req, _logger);
            if (!succeeded)
                return ActionResultHelper.BadRequest(message); 

            var result = await _authService.SignInAsync(formData!);
            return ActionResultHelper.CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignIn function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }

    [Function("SignOut")]
    public async Task<IActionResult> SignOut(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "auth/signout")] HttpRequest req)
    {
        try
        {
            var authHeader = req.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return new UnauthorizedResult();

            var (succeeded, data, _) = await RequestBodyHelper.ReadAndValidateRequestBody<SignOutRequest>(req, _logger);
            if (!succeeded || string.IsNullOrEmpty(data?.UserId))
                return ActionResultHelper.BadRequest("UserId is required");

            var result = await _authService.SignOutAsync(data.UserId);
            if (!result)
                return ActionResultHelper.BadRequest("Sign out failed");

            return ActionResultHelper.Ok(new { message = "Signed out successfully", Succeeded = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignOut function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }
    
    [Function("SignUp")]
    public async Task<IActionResult> SignUp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signup")] HttpRequest req)
    {
        try
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<SignUpFormDto>(req, _logger);
            if (!succeeded)
                return ActionResultHelper.BadRequest(message);

            var result = await _authService.SignUpAsync(formData!);
            return ActionResultHelper.CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignUp function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }
    

    // AccountService
    [Function("StartRegistration")]
    public async Task<IActionResult> StartRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/start-registration")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/start-registration";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("ConfirmEmailCode")]
    public async Task<IActionResult> ConfirmEmailCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/confirm-email-code")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/confirm-email-code";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("CompleteRegistration")]
    public async Task<IActionResult> CompleteRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/complete-registration")] HttpRequest req)
    {
        try
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<CompleteRegistrationFormDto>(req, _logger);
            if (!succeeded)
                return ActionResultHelper.BadRequest(message);

            var (accountResult, error) = await CompleteRegistrationHelper.CompleteAccountRegistrationAsync(formData, _configuration, _httpClient);
            if (accountResult == null)
                return ActionResultHelper.BadRequest(error);

            var userId = accountResult.Data?.User?.Id?.ToString();
            if (string.IsNullOrEmpty(userId))
                return ActionResultHelper.BadRequest("userId could not be extracted");

            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formData.Email);
            if (!tokenResult.Succeeded || string.IsNullOrEmpty(tokenResult.AccessToken))
                return ActionResultHelper.BadRequest("Internal server error: Could not generate access token.");

            return ActionResultHelper.Ok(new { succeeded = true, message = "Registration complete.", accessToken = tokenResult.AccessToken, user = accountResult.Data?.User });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CompleteRegistration function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }

    
    // 
    [Function("GetAccountById")]
    public async Task<IActionResult> GetAccountById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/account/{userId}")] HttpRequest req, string userId)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/{userId}";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Get, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("GetAccountByEmail")]
    public async Task<IActionResult> GetAccountByEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/account/by-email/{email}")] HttpRequest req, string email)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/by-email/{email}";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Get, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("GenerateNewEmailConfirmationToken")]
    public async Task<IActionResult> GenerateNewEmailConfirmationToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/account/{userId}/email-confirmation-token")] HttpRequest req, string userId)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/{userId}/email-confirmation-token";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("ValidateCredentials")]
    public async Task<IActionResult> ValidateCredentials(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/validate-credentials")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/validate-credentials";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("UpdateUser")]
    public async Task<IActionResult> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "auth/account/{userId}")] HttpRequest req, string userId)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/{userId}";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Put, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("DeleteAccount")]
    public async Task<IActionResult> DeleteAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "auth/account/{userId}")] HttpRequest req, string userId)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/{userId}";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Delete, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("ForgotPassword")]
    public async Task<IActionResult> ForgotPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/forgot-password")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/forgot-password";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    [Function("ResetPassword")]
    public async Task<IActionResult> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/reset-password")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:AccountServiceProvider"]}/api/accounts/reset-password";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.AccountService, _configuration, _httpClient, _logger);
    }

    // TokenService
    [Function("GenerateToken")]
    public async Task<IActionResult> GenerateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/generate-token")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:TokenServiceProvider"]}/api/generate-token";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.TokenService, _configuration, _httpClient, _logger);
    }

    [Function("ValidateToken")]
    public async Task<IActionResult> ValidateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/validate-token")] HttpRequest req)
    {
        var url = $"{_configuration["Providers:TokenServiceProvider"]}/api/validate-token";
        return await ProxyHelper.Proxy(req, url, HttpMethod.Post, ProxyHelper.ProxyTarget.TokenService, _configuration, _httpClient, _logger);
    }
}
