using AuthService.Api.DTOs;
using AuthService.Api.Services;
using AuthService.Api.Helpers;
using AuthService.Api.Models.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AuthService.Api.Functions;

public class AuthFunctions(ILogger<AuthFunctions> logger, IAuthService authService, IConfiguration configuration, HttpClient httpClient, ITokenServiceClient tokenServiceClient)
{
    private readonly ILogger<AuthFunctions> _logger = logger;
    private readonly IAuthService _authService = authService;
    private readonly IConfiguration _configuration = configuration;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenServiceClient _tokenServiceClient = tokenServiceClient;
    
    private Task<IActionResult> ProxyAccount(HttpRequest req, string relativePath, HttpMethod method, ProxyHelper.ProxyTarget target = ProxyHelper.ProxyTarget.AccountService)
    {
        var baseUrl = _configuration[
            target == ProxyHelper.ProxyTarget.AccountService
                ? "Providers:AccountServiceProvider"
                : "Providers:TokenServiceProvider"
        ];
        var url = $"{baseUrl}{relativePath}";
        return ProxyHelper.Proxy(req, url, method, target, _configuration, _httpClient, _logger);
    }

    [Function("SignIn")]
    public Task<IActionResult> SignIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signin")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(async () =>
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<SignInFormDto>(req, _logger);
            if (!succeeded)
                throw new ProblemException("INVALID_SIGNIN_DATA", 400, message);
            var result = await _authService.SignInAsync(formData!);
            return new OkObjectResult(result);
        }, req.HttpContext);
    }

    [Function("SignOut")]
    public Task<IActionResult> SignOut(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "auth/signout")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(async () =>
        {
            var authHeader = req.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                throw new ProblemException("INVALID_TOKEN", 401, "Authorization header is missing or invalid.");

            var (succeeded, data, _) = await RequestBodyHelper.ReadAndValidateRequestBody<SignOutRequest>(req, _logger);
            if (!succeeded || string.IsNullOrEmpty(data?.UserId))
                throw new ProblemException("INVALID_USERID", 400, "UserId is required");

            var result = await _authService.SignOutAsync(data.UserId);
            if (!result)
                throw new ProblemException("SIGNOUT_FAILED", 400, "Sign out failed");

            return new OkObjectResult(new { message = "Signed out successfully", Succeeded = true });
        }, req.HttpContext);
    }

    [Function("SignUp")]
    public Task<IActionResult> SignUp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signup")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(async () =>
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<SignUpFormDto>(req, _logger);
            if (!succeeded)
                throw new ProblemException("INVALID_SIGNUP_DATA", 400, message);
            var result = await _authService.SignUpAsync(formData!);
            return new OkObjectResult(result);
        }, req.HttpContext);
    }

    // proxies to AccountService
    [Function("StartRegistration")]
    public Task<IActionResult> StartRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/start-registration")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/accounts/start-registration", HttpMethod.Post), req.HttpContext);
    }

    [Function("ConfirmEmailCode")]
    public Task<IActionResult> ConfirmEmailCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/confirm-email-code")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/accounts/confirm-email-code", HttpMethod.Post), req.HttpContext);
    }

    [Function("CompleteRegistration")]
    public Task<IActionResult> CompleteRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/complete-registration")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(async () =>
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<CompleteRegistrationFormDto>(req, _logger);
            if (!succeeded)
                throw new ProblemException("INVALID_REGISTRATION_DATA", 400, message);

            var (accountResult, error) = await CompleteRegistrationHelper.CompleteAccountRegistrationAsync(formData, _configuration, _httpClient);
            if (accountResult == null)
                throw new ProblemException("ACCOUNT_REGISTRATION_FAILED", 400, error);

            var userId = accountResult.Data?.User?.Id?.ToString();
            if (string.IsNullOrEmpty(userId))
                throw new ProblemException("USERID_EXTRACTION_FAILED", 400, "userId could not be extracted");

            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formData?.Email);
            if (!tokenResult.Succeeded || string.IsNullOrEmpty(tokenResult.AccessToken))
                throw new ProblemException("TOKEN_GENERATION_FAILED", 500, "Internal server error: Could not generate access token.");

            return new OkObjectResult(new {
                succeeded = true,
                message = "Registration complete.",
                accessToken = tokenResult.AccessToken,
                refreshToken = tokenResult.RefreshToken,
                user = accountResult.Data?.User
            });
        }, req.HttpContext);
    }

    [Function("GetAccountById")]
    public Task<IActionResult> GetAccountById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/account/{userId}")] HttpRequest req, string userId)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, $"/api/accounts/{userId}", HttpMethod.Get), req.HttpContext);
    }

    [Function("GetAccountByEmail")]
    public Task<IActionResult> GetAccountByEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/account/by-email/{email}")] HttpRequest req, string email)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, $"/api/accounts/by-email/{email}", HttpMethod.Get), req.HttpContext);
    }

    [Function("GenerateNewEmailConfirmationToken")]
    public Task<IActionResult> GenerateNewEmailConfirmationToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/account/{userId}/email-confirmation-token")] HttpRequest req, string userId)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, $"/api/accounts/{userId}/email-confirmation-token", HttpMethod.Post), req.HttpContext);
    }

    [Function("ValidateCredentials")]
    public Task<IActionResult> ValidateCredentials(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/validate-credentials")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/accounts/validate-credentials", HttpMethod.Post), req.HttpContext);
    }

    [Function("UpdateUser")]
    public Task<IActionResult> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "auth/account/{userId}")] HttpRequest req, string userId)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, $"/api/accounts/{userId}", HttpMethod.Put), req.HttpContext);
    }

    [Function("DeleteAccount")]
    public Task<IActionResult> DeleteAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "auth/account/{userId}")] HttpRequest req, string userId)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, $"/api/accounts/{userId}", HttpMethod.Delete), req.HttpContext);
    }

    [Function("ForgotPassword")]
    public Task<IActionResult> ForgotPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/forgot-password")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/accounts/forgot-password", HttpMethod.Post), req.HttpContext);
    }

    [Function("ResetPassword")]
    public Task<IActionResult> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/reset-password")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/accounts/reset-password", HttpMethod.Post), req.HttpContext);
    }

    // Proxies to TokenService
    [Function("GenerateToken")]
    public Task<IActionResult> GenerateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/generate-token")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/generate-token", HttpMethod.Post, ProxyHelper.ProxyTarget.TokenService), req.HttpContext);
    }

    [Function("ValidateToken")]
    public Task<IActionResult> ValidateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/validate-token")] HttpRequest req)
    {
        return FunctionErrorWrapper.Handle(
            () => ProxyAccount(req, "/api/validate-token", HttpMethod.Post, ProxyHelper.ProxyTarget.TokenService), req.HttpContext);
    }
}
